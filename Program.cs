using System.Linq;
using System.Text;
using System.Text.Json;
using BackEnd.Services;
using BackEnd.Util;
using dotenv.net;
using dotenv.net.Utilities;
using Minio;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using BackEnd.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    var allowedOrigins = EnvReader.GetStringValue("CORS_ALLOWED_ORIGINS")
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(origin => origin.Trim().ToLower());

    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin => 
            { 
                // In 'dev' omgevingen (lokaal, Docker compose of de Azure 'dev' versie) zijn alle origins toegelaten.
                // In non-'dev' omgevingen (productie) is de lijst beperkt tot de opgegeven lijst.
                return builder.Environment.IsDevelopment() 
                       || 
                       allowedOrigins.Any(ao => origin.ToLower().StartsWith(ao)); 
            })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Add services to the container.
DotEnv.Load();

builder.Services.AddControllers()
    .AddJsonOptions(
        options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);


builder.Services.AddMinio(configureClient => 
        configureClient
            .WithEndpoint(EnvReader.GetStringValue("MINIO_ENDPOINT"))
            .WithCredentials(
                EnvReader.GetStringValue("MINIO_AK"), 
                EnvReader.GetStringValue("MINIO_SK"))
            .WithSSL(EnvReader.GetStringValue("MINIO_USE_SSL") == "true")
            .Build());

builder.Services.Configure<ChatfishDatabaseSettings>(
    builder.Configuration.GetSection("ChatfishDatabase"));

builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<PostService>();
builder.Services.AddSingleton<ChannelService>();
builder.Services.AddSingleton<StoryMessageService>();
builder.Services.AddSingleton<CommentService>();
builder.Services.AddSingleton<CharacterService>();
builder.Services.AddSingleton<ScenarioService>();
builder.Services.AddSingleton<TicketService>();
builder.Services.AddSingleton<PushNotificationService>();
builder.Services.AddSingleton<BackEnd.Services.WebSocketManager>();

builder.Services.AddHostedService<StoryMessagePollingWorker>();

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});
builder.Services.AddOpenApi();


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = EnvReader.GetStringValue("JWT_AUTHORITY");
        options.Audience = EnvReader.GetStringValue("JWT_AUDIENCE");
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        var secret = EnvReader.GetStringValue("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT secret not configured. Set the JWT_SECRET environment variable.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Authority,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.ContainsKey("jwt"))
                    context.Token = context.Request.Cookies["jwt"];
                return Task.CompletedTask;
            }
        };
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseWebSockets();

app.Run();
