using Itm.Discount.Api.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

var discountDB = new List<DiscountDTO>
{
    new ("ITM50", 0.5f),
    new ("ITM60", 0.6f),
    new ("ITM70", 0.7f)
};

app.MapGet("/api/discounts/{codigo}", (string codigo) =>
{
    var item = discountDB.FirstOrDefault(d => d.Codigo == codigo);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});

app.MapControllers();
app.Run();