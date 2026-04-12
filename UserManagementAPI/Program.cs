using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Threading;

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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(new { message = "An unexpected server error occurred." });
    });
});

app.UseHttpsRedirection();

var initialUsers = new User[]
{
    new() { Id = 1, FullName = "Anita Sharma", Email = "anita.sharma@techhive.local", Department = "HR", IsActive = true },
    new() { Id = 2, FullName = "Rahul Verma", Email = "rahul.verma@techhive.local", Department = "IT", IsActive = true }
};

var usersById = new ConcurrentDictionary<int, User>(initialUsers.ToDictionary(u => u.Id));
var emailToId = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var user in initialUsers)
{
    emailToId[NormalizeEmail(user.Email)] = user.Id;
}

var idCounter = usersById.Keys.DefaultIfEmpty(0).Max();

var usersApi = app.MapGroup("/api/users").WithTags("Users");

usersApi.MapGet("/", (string? department, bool? isActive, ILogger<Program> logger) =>
{
    try
    {
        IEnumerable<User> query = usersById.Values;

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(u => u.Department.Equals(department.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var result = query
            .OrderBy(u => u.Id)
            .Select(UserResponse.FromUser)
            .ToArray();

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error while getting users.");
        return Results.Problem("An unexpected error occurred while fetching users.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("GetAllUsers");

usersApi.MapGet("/{id:int}", (int id, ILogger<Program> logger) =>
{
    try
    {
        if (id <= 0)
        {
            return Results.BadRequest(new { message = "ID must be greater than zero." });
        }

        if (!usersById.TryGetValue(id, out var user))
        {
            return Results.NotFound(new { message = $"User with ID {id} was not found." });
        }

        return Results.Ok(UserResponse.FromUser(user));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error while getting user {UserId}.", id);
        return Results.Problem("An unexpected error occurred while fetching the user.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("GetUserById");

usersApi.MapPost("/", (CreateUserRequest request, ILogger<Program> logger) =>
{
    try
    {
        var validationErrors = UserValidator.Validate(request.FullName, request.Email, request.Department);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new { message = "Validation failed.", errors = validationErrors });
        }

        var normalizedEmail = NormalizeEmail(request.Email!);
        if (emailToId.ContainsKey(normalizedEmail))
        {
            return Results.Conflict(new { message = "A user with this email already exists." });
        }

        var nextId = Interlocked.Increment(ref idCounter);
        var user = new User
        {
            Id = nextId,
            FullName = request.FullName!.Trim(),
            Email = request.Email!.Trim(),
            Department = request.Department!.Trim(),
            IsActive = request.IsActive
        };

        if (!emailToId.TryAdd(normalizedEmail, user.Id))
        {
            return Results.Conflict(new { message = "A user with this email already exists." });
        }

        if (!usersById.TryAdd(user.Id, user))
        {
            emailToId.TryRemove(normalizedEmail, out _);
            return Results.Problem("Unable to create user at this time.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Created($"/api/users/{user.Id}", UserResponse.FromUser(user));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error while creating a user.");
        return Results.Problem("An unexpected error occurred while creating the user.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("CreateUser");

usersApi.MapPut("/{id:int}", (int id, UpdateUserRequest request, ILogger<Program> logger) =>
{
    try
    {
        if (id <= 0)
        {
            return Results.BadRequest(new { message = "ID must be greater than zero." });
        }

        var validationErrors = UserValidator.Validate(request.FullName, request.Email, request.Department);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new { message = "Validation failed.", errors = validationErrors });
        }

        if (!usersById.TryGetValue(id, out var existingUser))
        {
            return Results.NotFound(new { message = $"User with ID {id} was not found." });
        }

        var oldNormalizedEmail = NormalizeEmail(existingUser.Email);
        var newNormalizedEmail = NormalizeEmail(request.Email!);

        if (!newNormalizedEmail.Equals(oldNormalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (emailToId.TryGetValue(newNormalizedEmail, out var ownerId) && ownerId != id)
            {
                return Results.Conflict(new { message = "A different user with this email already exists." });
            }

            if (!emailToId.TryAdd(newNormalizedEmail, id))
            {
                return Results.Conflict(new { message = "A different user with this email already exists." });
            }
        }

        var updatedUser = new User
        {
            Id = id,
            FullName = request.FullName!.Trim(),
            Email = request.Email!.Trim(),
            Department = request.Department!.Trim(),
            IsActive = request.IsActive
        };

        usersById[id] = updatedUser;

        if (!newNormalizedEmail.Equals(oldNormalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            emailToId.TryRemove(oldNormalizedEmail, out _);
        }

        return Results.Ok(UserResponse.FromUser(updatedUser));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error while updating user {UserId}.", id);
        return Results.Problem("An unexpected error occurred while updating the user.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("UpdateUser");

usersApi.MapDelete("/{id:int}", (int id, ILogger<Program> logger) =>
{
    try
    {
        if (id <= 0)
        {
            return Results.BadRequest(new { message = "ID must be greater than zero." });
        }

        if (!usersById.TryRemove(id, out var removedUser))
        {
            return Results.NotFound(new { message = $"User with ID {id} was not found." });
        }

        emailToId.TryRemove(NormalizeEmail(removedUser.Email), out _);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error while deleting user {UserId}.", id);
        return Results.Problem("An unexpected error occurred while deleting the user.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("DeleteUser");

app.Run();

static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

record UserResponse(int Id, string FullName, string Email, string Department, bool IsActive)
{
    public static UserResponse FromUser(User user) => new(user.Id, user.FullName, user.Email, user.Department, user.IsActive);
}

record CreateUserRequest(string? FullName, string? Email, string? Department, bool IsActive);
record UpdateUserRequest(string? FullName, string? Email, string? Department, bool IsActive);

static class UserValidator
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static List<string> Validate(string? fullName, string? email, string? department)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add("FullName is required.");
        }
        else if (fullName.Trim().Length < 2)
        {
            errors.Add("FullName must be at least 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
        }
        else if (!EmailValidator.IsValid(email.Trim()))
        {
            errors.Add("Email must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            errors.Add("Department is required.");
        }

        return errors;
    }
}

