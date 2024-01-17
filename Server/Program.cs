using Blazored.LocalStorage;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using GameMaster.Web.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredLocalStorage(config => config.JsonSerializerOptions.WriteIndented = true);
builder.Services.AddScoped<State>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig()
{
    GatewayIntents = GatewayIntents.All
}));
builder.Services.AddSingleton<MafiaControlService>();
builder.Services.AddSingleton<MafiaCommandService>();
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddHostedService<BotService>();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


//app.MapBlazorHub();
//app.MapFallbackToPage("/_Host");

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapBlazorHub();
    endpoints.MapRazorPages();
    endpoints.MapFallbackToPage("/_Host");
});

if (app.Environment.IsDevelopment())
{
    app.Run();
}
else
{
    app.Run("https://localhost:443");
}