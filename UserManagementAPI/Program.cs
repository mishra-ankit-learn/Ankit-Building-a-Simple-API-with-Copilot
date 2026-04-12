var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var users = new List<User>
{
    new() { Id = 1, FullName = "Anita Sharma", Email = "anita.sharma@techhive.local", Department = "HR", IsActive = true },
    new() { Id = 2, FullName = "Rahul Verma", Email = "rahul.verma@techhive.local", Department = "IT", IsActive = true }
};

var usersApi = app.MapGroup("/api/users").WithTags("Users");

usersApi.MapGet("/", () => Results.Ok(users))
    .WithName("GetAllUsers");

usersApi.MapGet("/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is null ? Results.NotFound(new { message = $"User with ID {id} was not found." }) : Results.Ok(user);
})
    .WithName("GetUserById");

usersApi.MapPost("/", (CreateUserRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Department))
    {
        return Results.BadRequest(new { message = "FullName, Email, and Department are required." });
    }

    if (users.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    var nextId = users.Count == 0 ? 1 : users.Max(u => u.Id) + 1;
    var user = new User
    {
        Id = nextId,
        FullName = request.FullName.Trim(),
        Email = request.Email.Trim(),
        Department = request.Department.Trim(),
        IsActive = request.IsActive
    };

    users.Add(user);
    return Results.Created($"/api/users/{user.Id}", user);
})
    .WithName("CreateUser");

usersApi.MapPut("/{id:int}", (int id, UpdateUserRequest request) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null)
    {
        return Results.NotFound(new { message = $"User with ID {id} was not found." });
    }

    if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Department))
    {
        return Results.BadRequest(new { message = "FullName, Email, and Department are required." });
    }

    if (users.Any(u => u.Id != id && u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { message = "A different user with this email already exists." });
    }

    user.FullName = request.FullName.Trim();
    user.Email = request.Email.Trim();
    user.Department = request.Department.Trim();
    user.IsActive = request.IsActive;

    return Results.Ok(user);
})
    .WithName("UpdateUser");

usersApi.MapDelete("/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null)
    {
        return Results.NotFound(new { message = $"User with ID {id} was not found." });
    }

    users.Remove(user);
    return Results.NoContent();
})
    .WithName("DeleteUser");

app.Run();

class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

record CreateUserRequest(string FullName, string Email, string Department, bool IsActive);
record UpdateUserRequest(string FullName, string Email, string Department, bool IsActive);
