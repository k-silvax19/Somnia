using Somnia.Infraestrutura;
using Somnia.Infraestrutura.Compartilhado.Mensageria;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAvatarMessaging(builder.Configuration, consumidor: true);
await builder.Build().RunAsync();
