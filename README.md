# Clio Data Migrator

![Clio Logo](https://www.clio.com/wp-content/uploads/2020/07/Clio-Logo-1.png)

A WPF desktop application designed to securely migrate file-based data into Clio using the Clio API v4. The application handles large input files through streaming processing and implements OAuth 2.0 Authorization Code Grant flow for secure authentication.

## Features

- OAuth 2.0 authentication with Clio API
- Streaming file processing for efficient handling of large datasets
- Secure credential management
- Real-time progress tracking and logging
- Error handling and recovery mechanisms

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK:** .NET 8.0 or later. [Download here](https://dotnet.microsoft.com/download)

- **Development Environment:**

  - **Visual Studio 2022** with the ".NET desktop development" workload installed
  - **OR** Visual Studio Code with the C# extension

- **Clio Developer Account:** You need access to a Clio account with API access enabled and the following credentials:
  - Client ID
  - Client Secret
  - Website URL: `http://localhost:8888`
  - Redirect URI: `http://127.0.0.1:8888/callback`

## Setup

### 1. Clone or Download the Project

```bash
git clone <repository_url>
```

Or download the project files as a ZIP archive.

### 2. Open the Project

- **Visual Studio**: Open the `.sln` file (`Tool -- 2.sln`) in Visual Studio
- **VS Code**: Open the project folder in VS Code

### 3. Restore NuGet Packages

The application relies on several NuGet packages including:

- `System.Net.Http`
- `Microsoft.Extensions.Configuration`
- `CommunityToolkit.Mvvm`

These should restore automatically when you build, but you can manually restore if needed:

```bash
dotnet restore
```

### 4. Configure Clio API Credentials

Obtain your Client ID and Client Secret from your Clio developer portal:

1. Create an application in your Clio developer account
2. Set the Website URL to `http://localhost:8888`
3. Configure the Redirect URI to be `http://127.0.0.1:8888/callback`
4. Store your credentials securely using one of these methods:
   - Environment variables
   - `appsettings.json` (ensure it's in `.gitignore`)
   - User Secrets for development
   - DPAPI secure storage (implemented in `DpapiSecureStorage.cs`)

> ⚠️ **Security Warning**: Never hardcode sensitive credentials in source code files.

## Running the Application

### Building and Launching

#### Using Visual Studio 2022

1. Open the solution file (`MigrationTool.sln`)
2. Press `F5` or click the ▶️ Start button
3. Alternatively, go to `Build > Build Solution` (Ctrl+Shift+B) then `Debug > Start Debugging`

#### Using Command Line

```bash
# Build the project
dotnet build

# Run the application
dotnet run
```

### Using the Application

![Application Workflow](https://via.placeholder.com/800x400?text=Application+Workflow)

1. **Authentication**

   - Enter your Clio Client ID into [appsettings.json] in their respective values
   - Enter your Client Secret into [appsettings.json] in their respective values
   - Verify the Redirect URI is set to `http://127.0.0.1:8888/callback`
   - Click the "Authenticate" button
   - Your browser will open to authenticate with Clio
   - Authorize the application when prompted

2. **Select Data Source**

   - Click the "Browse..." button to select your data file
   - Supported formats: CSV, Excel, and other structured data formats
   - The file path will display in the interface

3. **Start Migration**
   - Once authenticated and a file is selected, the "CONFIRM" button becomes active
   - Click "CONFIRM" to start the migration process
   - Monitor the progress bar and log area for real-time updates

## Important Notes

- **Sensitive Data:** This application handles sensitive data. Ensure all credentials and sensitive information from the source file are handled securely in memory, during transmission (HTTPS), and are not logged or stored insecurely.

- **Error Handling:** The application includes basic error handling and retry mechanisms. Review and enhance these based on specific Clio API error codes and potential issues with your source data.

- **Logging:** Comprehensive logging is crucial for a migration project. Implement persistent logging to a file to track the entire process, especially successes, failures, and detailed error information.

- **Testing:** Thoroughly test the migration process with non-sensitive sample data before running it on your actual sensitive data. Test different scenarios, including malformed data, API errors, and large file volumes.

- **Resource Management:** Ensure `HttpClient`, `HttpRedirectHandler`, and any other disposable resources are properly disposed of to prevent resource leaks. The ViewModel's `Dispose` method (if implemented) and the `using` statement for `HttpRedirectHandler` are examples of this.

This README provides a guide to setting up and running your Clio Data Migrator project. As you continue development, you may want to update it with more specific details about your data file format, mapping rules, and any advanced features you implement.
