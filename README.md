# The Noli

The Noli is a Unity 6 narrative game prototype with chapter missions, dialogue,
artifacts, quizzes, minigames, save slots, classrooms, and engagement analytics.

## Development setup

- Unity Editor: `6000.4.5f1`
- Main entry scene: `Assets/Scenes/MainMenu.unity`
- Runtime code: `Assets/Scripts`
- Editable scenes and prefabs: `Assets/Scenes` and `Assets/Prefabs`
- Localized game content: `Assets/JSON Files`
- Unity Cloud Code deployment scripts: `Assets/CloudCode`

Open the project with the matching Unity version and use the committed Build
Settings scene list. UGS-backed account, classroom, Cloud Save, and Cloud Code
flows require the correct Unity project link and deployed services; repository
validation does not confirm the live UGS environment.

## Editor tools

The recurring project commands are under `Tools > The Noli`:

- `Mission Builder` creates mission assets, step prefabs, and JSON entries.
- `Open Deployment Window` opens Unity's UGS Deployment window.
- `Minigames` contains manual rebuild commands for the three minigame scenes.

Minigame rebuilds replace their target scene. Use them deliberately; ordinary
editor startup does not regenerate scenes.

## Build scenes

1. Main Menu
2. Mansion
3. Chapter Quiz
4. Librarian Dashboard
5. Teacher Classroom Dashboard
6. Social Interruption Minigame
7. Keeping Composure Minigame
8. Greeting Etiquette Minigame

## Static validation

From the repository root:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal
```

Unity generates these project files. If restore assets are missing, run the
matching command once without `--no-restore`. These builds check C# compilation
only; use Unity Edit Mode/Play Mode and device testing for scenes, UI, gameplay,
saving, Android, and live UGS behavior.
