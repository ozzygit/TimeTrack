# .NET 8 Upgrade Report

## Project target framework modifications

| Project name                                   | Old Target Framework | New Target Framework | Commits                   |
|:-----------------------------------------------|:--------------------:|:--------------------:|---------------------------|
| TimeTrack\TimeTrack.csproj                     | net472               | net8.0-windows       | 571c16ee                  |

## All commits

| Commit ID  | Description                                  |
|:-----------|:---------------------------------------------|
| 9111ea93   | Commit upgrade plan                          |
| 571c16ee   | Upgrade to .NET 8; remove legacy config and assembly info |

## Next steps

- Build and run the app on .NET 8 to verify runtime behavior.
- Update any NuGet packages to versions compatible with net8.0-windows if needed.
- Review WPF warnings or API changes and address them if they appear during build.

