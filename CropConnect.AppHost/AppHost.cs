var builder = DistributedApplication.CreateBuilder(args);

var apiGateway = builder.AddProject<Projects.CropConnect_ApiGateway>("apigateway")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.CropConnect_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiGateway)
    .WaitFor(apiGateway);

builder.Build().Run();
