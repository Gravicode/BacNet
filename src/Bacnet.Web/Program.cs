using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Bacnet.Models;
using Bacnet.Web;
using Bacnet.Web.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
var opt = new DbContextOptionsBuilder<DevicePerformanceDbContext>()
        .UseInMemoryDatabase("DevicePerformance")
        .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;
using var context = new DevicePerformanceDbContext(opt);

context.Database.EnsureCreated();
builder.Services.AddSingleton(context);
builder.Services.AddScoped<IDevicePerformanceService, DevicePerformanceService>();

var policyName = "all";
builder.Services.AddCors(options => options.AddPolicy(policyName,
builder => builder.AllowAnyHeader()
.AllowAnyOrigin()
.AllowAnyMethod()));


var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//After builder.Build()
app.UseCors(policyName);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapPost("/deviceperformance", async (DevicePerformance model) =>
{
    using (var scope = app.Services.CreateScope())
    {
        IDevicePerformanceService performanceService = scope.ServiceProvider.GetRequiredService<IDevicePerformanceService>();
        var record = await performanceService.CreateDevicePerformance(model);
        return record;
    }
})
//.WithName("DevicePerformance")
.WithOpenApi(); 

app.Run();

