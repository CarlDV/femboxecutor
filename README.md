# Femboxecutor

Velocity Executor is a WPF-based application designed for script execution that uses VelocityAPI. This project uses .NET 8 and includes the Monaco Editor for code editing.

## Prerequisites

Before building the project, ensure you have the following installed:

- .NET 8.0 SDK and up
- Windows Operating System (Required for WPF/Windows Forms support)

## Building the Project

1. Open a terminal or command prompt in the project root directory.
2. Run the following command to restore dependencies and build the application:

   dotnet build

3. The compiled executable will be located in:

   bin/Debug/net8.0-windows/VelocityExecutor.exe

## Running the Application

To run the application directly from the terminal, use:

   dotnet run --project VelocityExecutor.csproj

## Project Structure

- VelocityExecutor/ - Contains the main source code and XAML files.
- VelocityExecutor/Images/ - Application assets and resources.
- Monaco/ - The Monaco Editor web files.
- VelocityAPI.dll - Velocity API.

## Modifying the Project

### UI Changes
The user interface is built using WPF (XAML). You can modify the design by editing the .xaml files located in the VelocityExecutor folder.
- MainWindow.xaml: The main application window.
- App.xaml: Global resources and styles.

### Logic Changes
The application logic is written in C#. Corresponding .cs files contain the code-behind for the XAML views and other services.

### Adding Dependencies
Use the dotnet add package command to include new NuGet packages.

   dotnet add package <PackageName>