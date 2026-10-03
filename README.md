# RoomDemo — meanings change the world

Unity **6000.6.3f1**, URP, Input System. A fixed-position greybox demo of
observation → collection → interpretation → world change → automatic reaction.

## Play

Open `Assets/_Game/Content/Scenes/DemoRoom.unity` and enter Play Mode, or run
`Builds/StandaloneOSX/RoomDemo.app` after building.

- **Left click**: observe an object.
- **Right mouse drag**: look around; position stays fixed. Yaw ±80°, pitch ±45°.
- **N / Tab** or the Notebook button: open/close Notebook. **Esc** closes it.
- **Home**: reset view. **F3**: toggle the Domain debug panel.

1. Observe the Plant to discover **Plant + Growth**.
2. Open Notebook, select **Growth** and **Plant**, then **INTERPRET**.
   The plant grows and the book automatically falls from the shelf.
3. Close Notebook and observe the fallen Book. It reveals **Book + Far + Window**.
4. Select **Far** and **Window** in Notebook, then **INTERPRET**.
   The window changes to a distant landscape. Wind starts automatically: the plant
   sways, papers flutter and a procedural wind clip loops.

Window can also be observed independently. Book inspection is gated by
`book.fallen`. Invalid or repeated interpretations report a reason without adding
facts or refiring reactions. The MVP uses English labels to avoid adding font or
localization dependencies; the object and concept meanings follow the Chinese brief.

## Test, regenerate and build

```sh
python3 Tools/pipeline.py
```

The pipeline reuses a connected Editor for this exact project. If none is running,
it launches the installed Editor in official batch mode. It runs:

**compile → Domain tests → scene generation → PlayMode tests → scene validation → Build**.

Empty test suites, failed tests, compile failures, missing references and unsuccessful
build reports stop the pipeline. Results and logs are in `Logs/Pipeline/`;
`Logs/build-summary.json` records the output and BuildResult. The build is a desktop
release build, without a development Pipeline server.

For an explicit clean batch run, first close this project in Unity:

```sh
python3 Tools/pipeline.py --headless
```

Optional `--editor /absolute/path/to/Unity` overrides the macOS install path.
Do not launch a second Editor against an already-open project.

Independent Editor entry points:

```sh
unity command eval 'Game.Editor.DemoSceneBuilder.Build();' --project-path "$PWD"
unity command eval 'Game.Editor.CliBuild.ValidateScene();' --project-path "$PWD"
unity command eval 'Game.Editor.CliBuild.QueueBuild();' --project-path "$PWD"
# Poll Logs/build-summary.json until Queued changes to Succeeded or Failed.
```

The same static methods accept `-executeMethod` in batch mode. For example, with
this project closed in the Editor:

```sh
UNITY_EDITOR='/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity'
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Logs/EditMode.xml" -logFile "$PWD/Logs/EditMode.log"
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -executeMethod Game.Editor.DemoSceneBuilder.Build -logFile "$PWD/Logs/Scene.log"
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -executeMethod Game.Editor.CliBuild.BuildDemo -buildTarget StandaloneOSX -logFile "$PWD/Logs/Build.log"
```

The commands above are individual entry points; use `Tools/pipeline.py` for the
fail-fast ordering. Unity tests must omit `-quit` so the runner can write its report.
`ROOM_DEMO_BUILD_OUTPUT` optionally overrides the default build output.
BuildDemo supports macOS, Windows64 and Linux64 when their platform modules are installed.

## Architecture and adding content

- **Game.Domain**: typed string IDs, read-only state queries, Commands/Events,
  immutable rule definitions and a small synchronous RuleEngine.
- **Game.Application**: GameSession owns GameState, executes Commands and publishes
  exactly the returned ordered events through `EventRaised`.
- **Game.Unity**: input, UI, camera, audio and fact-driven presentation.
- **Game.Editor**: repeatable primitive/prefab/scene generation, validation and builds.
- **Game.Domain.Tests / Game.PlayMode.Tests**: 14 rule/session tests plus 3 scene tests.

Domain and Application asmdefs set `noEngineReferences: true`. GameState's sets are
private; external queries return read-only snapshots. Domain rules never inspect a
Transform, material, active flag or animation state. A Reaction marks its ID fired
before producing deduplicated facts. The engine scans until no unfired eligible
reaction remains, including reverse-ordered chains and cycles.

Rules and observation content live in `Runtime/Domain/Rules/DemoRules.cs`.
Add object/concept definitions, interpretation pairs or reaction rows there;
existing rule types need no engine changes. A new kind of visual behavior needs a
small Unity Presenter. An observation can specify required facts and reveal other
objects or concepts. Duplicate IDs/pairs and unknown references are rejected.

Interactive prefabs contain **Logic** (SceneEntity + Clickable + Collider) and
**Visual**. Replace meshes under Visual and configure Presenter references; preserve
Logic and IDs. The scene builder deliberately regenerates its own DemoRoom,
prefabs, materials and wind clip. Save manual changes elsewhere if they must survive
regeneration. It refuses to replace an unsaved open scene.

Notebook derives its rows from GameState and holds only open/selection UI state.
Presenters subscribe to fact events and query the resulting Domain state. Growth
and wind affect separate transform channels. Book fall is a deterministic visual
animation; physics is not the source of the `book.fallen` fact.

F3 shows known IDs, facts, fired reactions and the last six Domain events, making
rule failures distinguishable from presentation failures. No external art has been
added; provenance policy is recorded in `Assets/ThirdParty/LICENSES.md`.

## Verification scope

PlayMode tests load the saved scene, use camera raycasts to inspect objects, invoke
serialized Notebook buttons, check the complete world loop, reject an invalid UI
interpretation, and verify camera constraints. They assert growth, fallen book
position, far background, sound playback and ongoing plant sway. They do not replace
an actual window walkthrough or listening to the audio.

No save/load, free semantics, drag/drop, inventory, networking, rule editor or
third-party art pack is included in this MVP.

Verified on 2026-10-03: the connected-Editor CLI pipeline passed 14 EditMode and
3 PlayMode tests, generated/validated DemoRoom and built the macOS release with
0 errors. The standalone application was also completed using real mouse/keyboard
input, including the four facts and both reactions shown in F3. The three build
warnings concern the disabled Player Pipeline and two unused URP debug shaders.
The separate `--headless` route is provided but was not exercised in this run.
Unity also serialized template URP migration and shader-prefilter settings while
building; original user changes to project metadata and template script deletions
were preserved.
