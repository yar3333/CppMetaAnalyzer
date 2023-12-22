# CppMetaAnalyzer

A tool for C++ to find class members which can be moved from `public` to `protected`.

```
Usage: CppMetaAnalyzer <options>
Details:
        -S, --sources Path to directory contains `*.cpp` files or `*.cpp` file to process.

        -I, --include Path to include directory.

        -D, --define  Define like `-D MYA=MYB`.

        --vscode      Load config from `.vscode/c_cpp_properties.json`.

        --verbose     Show more info.
```
