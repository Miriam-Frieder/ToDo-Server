using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TodoApi;

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// JWT Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins, builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Load the connection string from appsettings.json and inject the TodoContext into the builder
builder.Services.AddDbContext<ToDoDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("ToDoDB"),
    new MySqlServerVersion(new Version(8, 0, 2))));

builder.Services.AddScoped<JwtService>();


//Add the Swagger generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Use the CORS policy
app.UseCors(MyAllowSpecificOrigins);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ToDo API");
        c.RoutePrefix = string.Empty;
    });
}

// Enable authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Define the endpoints

// GET: api/items
app.MapGet("/api/items", [Authorize] async (ToDoDbContext db) =>
    await db.Items.ToListAsync());

// GET: api/items/{id}
app.MapGet("/api/items/{id}", [Authorize] async (int id, ToDoDbContext db) =>
{
    return await db.Items.FindAsync(id) is Item item ? Results.Ok(item) : Results.NotFound();
});

// POST: api/items
app.MapPost("/api/items", [Authorize] async (Item item, ToDoDbContext db) =>
{
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/items/{item.Id}", item);
});

// PUT: api/items/{id}
app.MapPut("/api/items/{id}", [Authorize] async (int id, Item inputItem, ToDoDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();

    item.Name = inputItem.Name;
    item.IsComplete = inputItem.IsComplete;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

// DELETE: api/items/{id}
app.MapDelete("/api/items/{id}", [Authorize] async (int id, ToDoDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();

    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/login", (User loginUser, ToDoDbContext db, JwtService jwtService) =>
{
    var user = db.Users.FirstOrDefault(u => u.Name == loginUser.Name && u.Password == loginUser.Password);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var token = jwtService.GenerateToken(user);
    return Results.Ok(new { token, id = loginUser?.Id });
});

app.MapPost("/api/register", async (User newUser, ToDoDbContext db) =>
{
    if (db.Users.Any(u => u.Name == newUser.Name))
    {
        return Results.BadRequest("User already exists.");
    }
    Console.WriteLine(newUser);
    db.Users.Add(newUser);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "User registered successfully" });
});

app.MapGet("/", () => "Server is running");


// Run the application
app.Run();
