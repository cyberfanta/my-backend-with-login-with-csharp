using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyBackendApi.Data;
using MyBackendApi.Services;
using Swashbuckle.AspNetCore.Annotations;
using MyBackendApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers(options =>
{
    // Configurar formatters para manejar correctamente la respuesta de archivos binarios
    options.FormatterMappings.SetMediaTypeMappingForFormat(
        "xlsx", 
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    );
});

// Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "My Backend API", 
        Version = "v1",
        Description = "RESTful API with authentication system and user management",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@mybackendapi.com"
        }
    });
    
    // Configure authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable Swagger annotations
    c.EnableAnnotations();
    
    // Use custom tag names from operation attributes instead of controller names
    c.TagActionsBy(api => {
        if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
        {
            // First check for Tags in SwaggerOperation attribute
            var operationAttrs = controllerActionDescriptor.MethodInfo
                .GetCustomAttributes(true)
                .OfType<SwaggerOperationAttribute>();
                
            var operationTags = operationAttrs
                .Where(attr => attr.Tags != null)
                .SelectMany(attr => attr.Tags)
                .ToList();
                
            if (operationTags.Any()) 
                return operationTags;
                
            // If no operation tags, use the controller's SwaggerTag
            var controllerTagAttrs = controllerActionDescriptor.ControllerTypeInfo
                .GetCustomAttributes(true)
                .OfType<SwaggerTagAttribute>();
                
            var controllerTags = controllerTagAttrs
                .Select(attr => attr.Description)
                .Where(tag => !string.IsNullOrEmpty(tag))
                .ToList();
                
            if (controllerTags.Any())
                return controllerTags;
        }
        
        // Fall back to controller name if no tags found
        return new[] { api.ActionDescriptor.RouteValues["controller"] };
    });
    
    // Add tag descriptions
    c.DocumentFilter<CustomTagDescriptions>();
    
    c.OrderActionsBy(api => api.RelativePath);
});

// Configure SQL Server database
// Note: We use a connection string for Docker with failure resilience
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), 
    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "defaultsecretkey12345678901234567890");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    
    // Configure to allow Swagger to receive the token
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    };
});

// Register services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PdfAnalyzerService>();
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<ExcelDocumentService>();

var app = builder.Build();

// Create/update the database asynchronously
Task.Run(async () =>
{
    try
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("DatabaseInitialization");
        await DatabaseInitializer.InitializeDatabaseAsync(app.Services, logger);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error initializing database: {ex.Message}");
    }
}).Wait();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "api/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/v1/swagger.json", "My Backend API v1");
        options.RoutePrefix = "api";
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        options.EnableDeepLinking();
        options.DisplayRequestDuration();
        options.DefaultModelsExpandDepth(0); // Hide schemas by default
        options.EnableFilter(); // Enable search 
        options.EnableTryItOutByDefault(); // Enable TryItOut by default
        
        // Añadir script personalizado para capturar el token JWT después del login
        options.InjectJavascript(@"
            setTimeout(function() {
                let loginEndpoint = '/auth/login';
                let authBtn = document.querySelector('.btn.authorize');
                
                // Capturar respuestas de la API
                (function(open) {
                    XMLHttpRequest.prototype.open = function() {
                        this.addEventListener('readystatechange', function() {
                            if (this.readyState === 4 && this.status === 200) {
                                try {
                                    // Verificar que sea la respuesta de login
                                    let url = this.responseURL;
                                    if (url && url.endsWith(loginEndpoint)) {
                                        let data = JSON.parse(this.responseText);
                                        if (data && data.token) {
                                            console.log('Token capturado automáticamente');
                                            
                                            // Clic en el botón de autorización
                                            if (authBtn) authBtn.click();
                                            
                                            // Esperar a que se abra el modal
                                            setTimeout(function() {
                                                // Insertar el token en el campo
                                                let inputField = document.querySelector('.auth-container input[type=text]');
                                                if (inputField) {
                                                    inputField.value = data.token;
                                                    
                                                    // Clic en el botón Authorize
                                                    let authorizeBtn = document.querySelector('.auth-btn-wrapper button.authorize');
                                                    if (authorizeBtn) authorizeBtn.click();
                                                    
                                                    // Cerrar el modal
                                                    setTimeout(function() {
                                                        let closeBtn = document.querySelector('.auth-btn-wrapper button.btn-done');
                                                        if (closeBtn) closeBtn.click();
                                                    }, 500);
                                                }
                                            }, 500);
                                        }
                                    }
                                } catch (e) {
                                    console.error('Error al procesar la respuesta:', e);
                                }
                            }
                        });
                        open.apply(this, arguments);
                    };
                })(XMLHttpRequest.prototype.open);
            }, 1000);
        ");
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run(); 