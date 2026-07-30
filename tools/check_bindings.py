#!/usr/bin/env python3
"""Catches XAML binding mistakes the C# compiler cannot see.

WPF resolves bindings at run time, so a binding onto a property that cannot be
written only blows up once the window is shown. The worst offender is the set of
dependency properties that bind TwoWay *by default*: pointing one of those at a
view-model property with a private setter throws

    A TwoWay or OneWayToSource binding cannot work on the read-only property 'X'

Run this before committing:

    python3 tools/check_bindings.py

Exits non-zero when something is wrong, so it also works as a CI step.
"""

import glob
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")

# Dependency properties whose metadata sets BindsTwoWayByDefault.
TWO_WAY_BY_DEFAULT = {
    ("ProgressBar", "Value"), ("Slider", "Value"), ("ScrollBar", "Value"),
    ("TextBox", "Text"), ("RichTextBox", "Document"), ("PasswordBox", "Password"),
    ("ComboBox", "Text"), ("ComboBox", "SelectedItem"), ("ComboBox", "SelectedIndex"),
    ("ComboBox", "SelectedValue"),
    ("ListBox", "SelectedItem"), ("ListBox", "SelectedIndex"), ("ListBox", "SelectedValue"),
    ("ListView", "SelectedItem"), ("DataGrid", "SelectedItem"),
    ("ToggleButton", "IsChecked"), ("CheckBox", "IsChecked"), ("RadioButton", "IsChecked"),
    ("Expander", "IsExpanded"), ("TabControl", "SelectedItem"), ("TabControl", "SelectedIndex"),
    ("DatePicker", "SelectedDate"), ("Calendar", "SelectedDate"),
    ("TreeViewItem", "IsSelected"), ("TreeViewItem", "IsExpanded"),
}

ELEMENT = re.compile(r"<(\w+)\b((?:[^<>]|\"[^\"]*\")*?)/?>", re.S)
BOUND_ATTR = re.compile(r"(\w+)\s*=\s*\"(\{Binding[^\"]*\})\"")
BINDING_PATH = re.compile(r"\{Binding\s+(?:Path=)?([A-Za-z_][\w]*)")


def read_only_properties():
    """Public properties across the project that a binding cannot write to."""
    names = set()

    for path in glob.glob(os.path.join(ROOT, "**", "*.cs"), recursive=True):
        if f"{os.sep}obj{os.sep}" in path or f"{os.sep}bin{os.sep}" in path:
            continue
        source = open(path, encoding="utf-8").read()

        # get-only expression-bodied properties, and ones with a non-public setter
        names |= set(re.findall(r"public\s+[\w<>?\[\],\.]+\s+(\w+)\s*=>", source))
        names |= set(re.findall(
            r"public\s+[\w<>?\[\],\.]+\s+(\w+)\s*\{[^{}]*?(?:private|protected|internal)\s+set",
            source, re.S))
        names |= set(re.findall(
            r"public\s+[\w<>?\[\],\.]+\s+(\w+)\s*\{\s*get\s*;\s*\}", source))

    return names


def main():
    read_only = read_only_properties()
    problems = []

    for path in sorted(glob.glob(os.path.join(ROOT, "**", "*.xaml"), recursive=True)):
        text = open(path, encoding="utf-8").read()

        for element in ELEMENT.finditer(text):
            tag, attributes = element.group(1), element.group(2)

            for prop, expression in BOUND_ATTR.findall(attributes):
                if (tag, prop) not in TWO_WAY_BY_DEFAULT or "Mode=" in expression:
                    continue

                target = BINDING_PATH.match(expression)
                name = target.group(1) if target else None
                if name in read_only:
                    line = text[:element.start()].count("\n") + 1
                    problems.append((os.path.relpath(path), line, tag, prop, name))

    if not problems:
        print("bindings ok - no two-way default onto a read-only property")
        return 0

    print("XAML bindings that will throw at run time:\n")
    for path, line, tag, prop, name in problems:
        print(f"  {path}:{line}  <{tag} {prop}=\"{{Binding {name}}}\">")
        print(f"      {name} has no public setter; add Mode=OneWay\n")

    return 1


if __name__ == "__main__":
    sys.exit(main())
