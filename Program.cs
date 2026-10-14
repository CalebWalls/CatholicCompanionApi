using CatholicCompanion.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ISaintOfTheDayService, SaintOfTheDayService>(client =>
{
    client.BaseAddress = new Uri("https://www.vaticannews.va/en/saints/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CatholicCompanion/1.0");
});
builder.Services.AddHttpClient<UsccbClient>(client =>
{
    client.BaseAddress = new Uri("https://bible.usccb.org/bible/readings/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CatholicCompanion/1.0");
});
builder.Services.AddTransient<ILiturgicalDateService, LiturgicalDateService>();
builder.Services.AddTransient<IDailyReadingsService, DailyReadingsService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var origins = builder.Environment.IsDevelopment()
    ? new[] { "http://localhost:4200", "http://127.0.0.1:4200" }
    : new[] { "https://catholiccompanion.azurewebsites.net" };

builder.Services.AddCors(options => options.AddPolicy("CompanionUi",
    policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("CompanionUi");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
