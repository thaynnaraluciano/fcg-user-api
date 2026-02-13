using Api.Extensions;
using Api.Services;
using Api.Utils;
using CrossCutting.Configuration;
using CrossCutting.Exceptions.Middlewares;
using CrossCutting.Monitoring;
using Domain.Commands.v1.Login;
using Domain.Commands.v1.Usuarios.AlterarStatusUsuario;
using Domain.Commands.v1.Usuarios.AtualizarUsuario;
using Domain.Commands.v1.Usuarios.BuscarUsuarioPorId;
using Domain.Commands.v1.Usuarios.CriarSenha;
using Domain.Commands.v1.Usuarios.CriarUsuario;
using Domain.Commands.v1.Usuarios.ListarUsuarios;
using Domain.Commands.v1.Usuarios.RemoverUsuario;
using Domain.MapperProfiles;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Data.Interfaces.Usuarios;
using Infrastructure.Data.Repositories.Usuarios;
using Infrastructure.Messaging.Configuration;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Services.Interfaces.v1;
using Infrastructure.Services.Services.v1;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Prometheus;
using Prometheus.DotNetRuntime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FIAP Cloud Games - User API",
        Version = "v1",
        Description = @"
            FIAP Cloud Games é uma plataforma de venda de jogos digitais e gestão de servidores para partidas online.

            - Funcionalidades principais: gerenciamento de jogos, usuários, promoções e envio de notificações por e-mail.

            - Usuários: cadastro, criação inicial de senha, gerenciamento de status e dados dos usuários."
    });
});

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

#region RABBIT MQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GameAvailableConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        MassTransitConfiguration.Configure(context, cfg);
    });
});

#endregion

#region MediatR
// Login
//builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly));
//Usuarios
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(CriarUsuarioCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(AtualizarUsuarioCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(RemoverUsuarioCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ListarUsuariosCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(BuscarUsuarioPorIdCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(AlterarStatusUsuarioCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(CriarSenhaCommandHandler).Assembly));
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly));

#endregion

#region AutoMapper
builder.Services.AddAutoMapper(typeof(UsuarioProfile));
#endregion

#region Validators
// Login
//builder.Services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
// Usuário
builder.Services.AddScoped<IValidator<CriarUsuarioCommand>, CriarUsuarioCommandValidator>();
builder.Services.AddScoped<IValidator<AtualizarUsuarioCommand>, AtualizarUsuarioCommandValidator>();
builder.Services.AddScoped<IValidator<RemoverUsuarioCommand>, RemoverUsuarioCommandValidator>();
builder.Services.AddScoped<IValidator<BuscarUsuarioPorIdCommand>, BuscarUsuarioPorIdCommandValidator>();
builder.Services.AddScoped<IValidator<AlterarStatusUsuarioCommand>, AlterarStatusUsuarioCommandValidator>();
builder.Services.AddScoped<IValidator<ListarUsuariosCommand>, ListarUsuariosCommandValidator>();
builder.Services.AddScoped<IValidator<CriarSenhaCommand>, CriarSenhaCommandValidator>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
#endregion

#region Interfaces
builder.Services.AddSingleton<ICriptografiaService, CriptografiaService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
#endregion

builder.Services.Configure<AppSettings>(builder.Configuration);
builder.Services.AddSingleton<IMetricsService, MetricsService>();

builder.Configuration.AddEnvironmentVariables();

#if DEBUG
//Chama o gerenciador do docker ANTES da aplicação iniciar
string connString = builder.Configuration.GetConnectionString(name: "DefaultConnection")??"";
await DockerMySqlManager.EnsureMySqlContainerRunningAsync(connString);
#else
    try
    {
        Env.Load();
    }
    catch
    {
        //Caso do deploy.
        Console.WriteLine(".env não encontrado, usando apenas variáveis de ambiente...");
    }

    // Le variáveis de ambiente (do SO, .env ou secrets)
    string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
    string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "testdb";
    string user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
    string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

    string connString = $"Server={host};Database={db};User={user};Password={pass};";

#endif
builder.Services.AddDbContext<AppDbContext>(options => options.UseMySql
(
    connString,
    new MySqlServerVersion(new Version(8, 0, 43))
));
var app = builder.Build();
#if DEBUG
//Aguardando docker subir.
await Infrastructure.Data.MigrationHelper.WaitForMySqlAsync(connString);
//Aplica migrations se não estiver atualizado
Infrastructure.Data.MigrationHelper.ApplyMigrations(app);

#endif

// Para rodar com API Gateway na AWS, usar caminho base /payment
app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = "/user" }
        };
    });
});

app.UseSwaggerUI();

app.UseReDoc(c =>
{
    c.DocumentTitle = "REDOC API Documentation";
    c.SpecUrl = "/swagger/v1/swagger.json";
});

app.UseHttpsRedirection();

DotNetRuntimeStatsBuilder.Default().StartCollecting();

app.UseRouting();

app.UseRequestMetrics();
app.MapMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.UseMiddleware<MiddlewareTratamentoDeExcecoes>();

app.Run();