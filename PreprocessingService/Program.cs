using PreprocessingService;

var builder = WebApplication.CreateBuilder(args);

// Regista o servi�o gRPC no pipeline
builder.Services.AddGrpc();

var app = builder.Build();

// Mapeia o servi�o Preprocessor definido no teu .proto
app.MapGrpcService<PreprocessorService>();

// Endpoint de teste para confirmar que o servi�o est� ativo
app.MapGet("/", () => "Servi�o de Pr�-Processamento gRPC ativo.");

app.Run();