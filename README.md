# Ryujinx.CustomTasks

A collection of custom MSBuild tasks.

## How do I add this Nuget package?

Make sure to include the package with `PrivateAssets` set to `all`.

```cs
<PackageReference Include="Ryujinx.CustomTasks" Version="1.0.0" PrivateAssets="all" />
```

## Tasks

### GenerateArrays

This task scans source files for StructArray (i.e. `Array16<byte>`) types and generates them.

#### Input properties:

- `ArrayNamespace`: Namespace to be used by generated files.
  - Required

- `ArrayOutputPath`: Directory used to save the generated files in.
  - Required

- `ArrayInputFiles`: List of files to be searched for StructArray types.
  - Required
  - Default: All source files of the current project.
