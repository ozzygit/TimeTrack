# TimeTrack Project Rules

These rules apply to the `ozzygit/TimeTrack` repository.
Universal rules are in `%APPDATA%\devin\AGENTS.md` (Windows) or
`~/.config/devin/AGENTS.md` (Linux/macOS).

## Project context
- TimeTrack is a C# / .NET WPF desktop application.
- Output files (CSV, TXT, DOCX, MD) must be saved outside the git repo per global rules.

## C# / .NET coding conventions
- Prefer `var` only when the type is obvious from the right-hand side.
- Use `async`/`await` for I/O-bound and long-running operations.
- Avoid hard-coded connection strings, file paths, or secrets; use configuration or prompt the user.
- Follow standard C# naming: PascalCase for methods/properties/classes, camelCase for local variables and parameters.
- Keep UI logic (ViewModels/Views) separate from business logic and data access where practical.

## Secrets and credentials
Do not commit secrets, keys, tokens, passwords, or private key material. This is a **public** repository — do not include Tenant IDs, Client IDs, Certificate Thumbprints, or any other infrastructure identifiers.
