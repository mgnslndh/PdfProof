// Builds, tests and packs PdfProof exactly as CI does; CI runs this build, so the two can't drift apart.
// Arguments: --target=<task> (Default = GenerateFixtures, FetchCorpus, BuildTests, Build, Test, Pack),
// --configuration=<c> (Release), --require-fixtures, --skip-generate, --skip-fetch, --producers=<a,b>.
var target = Argument("target", "Default");

Setup(context => BuildParameters.Create(context));

RegisterBuildTasks();
RegisterTestTasks();
RegisterFixtureTasks();
RegisterCorpusTasks();
RegisterPackTasks();
RegisterDefaultTask();

RunTarget(target);
