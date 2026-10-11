// Builds, tests and packs PdfProof exactly as CI does; CI runs this build, so the two can't drift apart.
// Arguments: --target=<task> (Default = BuildTests, Build, Test, Pack), --configuration=<c> (Release).
var target = Argument("target", "Default");

Setup(context => BuildParameters.Create(context));

RegisterBuildTasks();
RegisterTestTasks();
RegisterPackTasks();
RegisterDefaultTask();

RunTarget(target);
