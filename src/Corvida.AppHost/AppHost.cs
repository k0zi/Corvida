var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var db = postgres.AddDatabase("corvida-db");

builder.AddProject<Projects.Corvida_Api>("corvida-api")
    .WithReference(db, "CorvidaApi")
    .WaitFor(db);

builder.Build().Run();
