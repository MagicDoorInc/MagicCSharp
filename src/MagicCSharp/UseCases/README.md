# MagicCSharp

A foundational library for building Enterprise-grade C# applications with clean architecture patterns.

## Features

- **IMagicUseCase Pattern**: Define use cases with a clean, marker-interface pattern
- **Automatic DI Registration**: Scan assemblies and automatically register all use cases
- **Flexible Lifetime Management**: Configure service lifetimes (Scoped, Transient, Singleton) via attributes
- **Clean Architecture**: Promotes separation of concerns and testable code

## Installation

```bash
dotnet add package MagicCSharp
```

## Usage

### 1. Define a Use Case Interface

```csharp
using MagicCSharp.UseCases;

public interface ICreateUserUseCase : IMagicUseCase
{
    Task<User> Execute(CreateUserRequest request);
}
```

### 2. Implement the Use Case

```csharp
using MagicCSharp.UseCases;

[MagicUseCase] // Defaults to Scoped lifetime
public class CreateUserUseCase : ICreateUserUseCase
{
    private readonly IUserRepository _repository;

    public CreateUserUseCase(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User> Execute(CreateUserRequest request)
    {
        // Implementation
        return await _repository.CreateAsync(request);
    }
}
```

### 3. Register All Use Cases

In your `Program.cs`:

```csharp
using MagicCSharp.UseCases;

var builder = WebApplication.CreateBuilder(args);

// Register all use cases from the executing assembly
builder.Services.AddMagicUseCases(Assembly.GetExecutingAssembly());

var app = builder.Build();
app.Run();
```

### 4. Inject and Use

```csharp
public class UserController : ControllerBase
{
    private readonly ICreateUserUseCase _createUserUseCase;

    public UserController(ICreateUserUseCase createUserUseCase)
    {
        _createUserUseCase = createUserUseCase;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var user = await _createUserUseCase.Execute(request);
        return Ok(user);
    }
}
```

## Configuring Service Lifetime

You can specify a different service lifetime using the attribute:

```csharp
[MagicUseCase(ServiceLifetime.Singleton)]
public class CachedDataUseCase : ICachedDataUseCase
{
    // Implementation
}
```

## License

MIT
