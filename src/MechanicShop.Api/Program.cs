var builder = WebApplication.CreateBuilder(args);
// DI container 
var app = builder.Build();
// Middlewares + endpoints
app.MapGet("/", () => "Hello World!");

app.Run();


// top level statement approach create Program class internal and main method inside it 
// so, in integration test we can not access Program class because it is will be internal 
// so we do this 
// Makes the generated entry point accessible to the integration test assembly.
public partial class Program;
