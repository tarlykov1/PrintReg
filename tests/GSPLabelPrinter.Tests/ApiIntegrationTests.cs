using System.Net;
using System.Net.Http.Json;
using System.Text;
using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;
using GSPLabelPrinter.Printing;
using GSPLabelPrinter.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GSPLabelPrinter.Tests;

public sealed class ApiIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        File.WriteAllText(Path.Combine(_root, "data", "employees.csv"), "ФИО;Должность\nИванов Иван;Главный специалист\n", new UTF8Encoding(true));
        Environment.SetEnvironmentVariable(AppEnvironment.AppRootVariable, _root);
        Environment.SetEnvironmentVariable(AppEnvironment.TestModeVariable, "true");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(_root);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPrinterService>();
                services.AddSingleton<IPrinterService, FakePrinterService>();
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact] public async Task HealthReturnsOk(){var response=await _client.GetAsync("/api/health");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("employees",await response.Content.ReadAsStringAsync());}
    [Fact] public async Task SearchFindsEmployee(){var rows=await _client.GetFromJsonAsync<List<Employee>>("/api/employees/search?q=иван");Assert.Single(rows!);}
    [Fact] public async Task SearchWithOneCharacterReturnsEmpty(){var rows=await _client.GetFromJsonAsync<List<Employee>>("/api/employees/search?q=и");Assert.Empty(rows!);}
    [Fact] public async Task AddEmployeeWorks(){var response=await _client.PostAsJsonAsync("/api/employees",new AddEmployeeRequest("Сергеев Сергей","Инженер"));Assert.Equal(HttpStatusCode.OK,response.StatusCode);}
    [Fact] public async Task DuplicateReturnsConflict(){await _client.PostAsJsonAsync("/api/employees",new AddEmployeeRequest("Дубль","Инженер"));var response=await _client.PostAsJsonAsync("/api/employees",new AddEmployeeRequest("дубль","Инженер"));Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);}
    [Fact] public async Task MalformedJsonReturnsBadRequest(){var response=await _client.PostAsync("/api/employees",new StringContent("{",Encoding.UTF8,"application/json"));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task EmptyFullNameReturnsBadRequest(){var response=await _client.PostAsJsonAsync("/api/employees",new AddEmployeeRequest(" ","Должность"));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task EmptyPositionReturnsBadRequest(){var response=await _client.PostAsJsonAsync("/api/employees",new AddEmployeeRequest("ФИО"," "));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task PrintCopiesZeroReturnsBadRequest(){var response=await _client.PostAsJsonAsync("/api/print",new PrintRequest("ФИО","Должность",0));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task PrintCopiesElevenReturnsBadRequest(){var response=await _client.PostAsJsonAsync("/api/print",new PrintRequest("ФИО","Должность",11));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task PrintWithoutPrinterReturnsBadRequest(){var response=await _client.PostAsJsonAsync("/api/print",new PrintRequest("ФИО","Должность",1));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task SettingsReturnsOk(){var response=await _client.GetAsync("/api/settings");Assert.Equal(HttpStatusCode.OK,response.StatusCode);}
    [Fact] public async Task InvalidSettingsReturnsBadRequest(){var settings=await _client.GetFromJsonAsync<AppSettings>("/api/settings");settings!.Printing.Copies=0;var response=await _client.PutAsJsonAsync("/api/settings",settings);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Environment.SetEnvironmentVariable(AppEnvironment.AppRootVariable, null);
        Environment.SetEnvironmentVariable(AppEnvironment.TestModeVariable, null);
        try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class FakePrinterService : IPrinterService
    {
        public IReadOnlyList<PrinterInfo> GetPrinters(string selectedPrinter) => [];
        public Task<(bool ok, string message, string code)> PrintAsync(Employee employee, int copies, PrintingSettings settings, CancellationToken ct = default) =>
            Task.FromResult((false, "Выберите принтер в настройках.", "PRINTER_NOT_SELECTED"));
    }
}
