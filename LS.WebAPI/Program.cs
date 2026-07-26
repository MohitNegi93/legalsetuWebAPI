var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://legal-nine-sooty.vercel.app/",
                "https://legalsetuwebapi-1.onrender.com/api/Chat"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
// Add Swagger for easy API testing in the browser
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient for making REST calls to the Gemini API
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ReactPolicy");
app.UseAuthorization();

app.MapControllers();

app.Run();
