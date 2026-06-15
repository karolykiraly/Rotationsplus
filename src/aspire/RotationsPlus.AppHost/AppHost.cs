// Local dev orchestration (Plan_Architecture.md §3.2): spins up Postgres and runs api + worker
// against it with one `dotnet run`. Requires Docker locally for the Postgres container.
// Not used in Azure — DEV/PREPROD/PROD are deployed via Bicep + pipelines.

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

// Database name must match the connection name api + worker read: "rotationsdb".
var rotationsdb = postgres.AddDatabase("rotationsdb");

builder.AddProject<Projects.RotationsPlus_Api>("rplus-api")
    .WithReference(rotationsdb)
    .WaitFor(rotationsdb);

builder.AddProject<Projects.RotationsPlus_Worker>("rplus-worker")
    .WithReference(rotationsdb)
    .WaitFor(rotationsdb);

builder.Build().Run();
