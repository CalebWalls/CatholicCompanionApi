using CatholicCompanion.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddTransient<ILiturgicalDateService, LiturgicalDateService>();
builder.Services.AddTransient<IDailyReadingsService, DailyReadingsService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (!builder.Environment.IsDevelopment())
{

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("MyPolicy",
            builder =>
            {
                //change this once deployed
                builder.WithOrigins("https://studysphereedu.azurewebsites.net")
                       .AllowAnyHeader()
                       .AllowAnyMethod();
            });
    });

}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("MyPolicy",
            builder =>
            {
                builder.WithOrigins("http://localhost:4200")
                       .AllowAnyHeader()
                       .AllowAnyMethod();
            });
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("MyPolicy"); // Apply the CORS policy

app.UseAuthorization();

app.MapControllers();

app.Run();
