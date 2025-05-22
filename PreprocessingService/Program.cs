using PreprocessingService;

var builder = WebApplication.CreateBuilder(args);

// Regista o serviço gRPC no pipeline
builder.Services.AddGrpc();

var app = builder.Build();

// Mapeia o serviço Preprocessor definido no teu .proto
app.MapGrpcService<PreprocessorService>();

// Endpoint de teste para confirmar que o serviço está ativo
app.MapGet("/", () => "Serviço de Pré-Processamento gRPC ativo.");

app.Run();