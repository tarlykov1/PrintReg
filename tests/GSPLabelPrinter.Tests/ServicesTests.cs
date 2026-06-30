using System.Text;
using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;
using GSPLabelPrinter.Printing;
using GSPLabelPrinter.Services;
using GSPLabelPrinter.Utilities;
using Xunit;

namespace GSPLabelPrinter.Tests;

public sealed class ServicesTests
{
    private static string Temp() { var p=Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    private static EmployeeCsvService Csv(string root) => new(new CsvBackupService(new FileLogger(root)), new FileLogger(root));

    [Fact] public void ReadsCorrectCsvWithSemicolon(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nИванов Иван;Главный специалист\n",new UTF8Encoding(true));Assert.Single(Csv(r).ReadAll(p));}
    [Fact] public void ReadsCsvWithBom(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nПетров Петр;Инженер\n",new UTF8Encoding(true));Assert.Equal("Петров Петр",Csv(r).ReadAll(p)[0].FullName);}
    [Fact] public void ReadsCsvWithoutBom(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nСидоров Сидор;Мастер\n",new UTF8Encoding(false));Assert.Equal("Мастер",Csv(r).ReadAll(p)[0].Position);}
    [Fact] public void ReadsEscapedQuotes(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\n\"Иванов \"\"Иван\"\"\";Специалист\n",Encoding.UTF8);Assert.Contains("\"Иван\"",Csv(r).ReadAll(p)[0].FullName);}
    [Fact] public void ReadsValueWithSemicolon(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nИванов;\"Специалист; эксперт\"\n",Encoding.UTF8);Assert.Equal("Специалист; эксперт",Csv(r).ReadAll(p)[0].Position);}

    [Fact] public void ReadsMultilineValue(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nИванов;\"Главный\nспециалист\"\n",Encoding.UTF8);Assert.Contains("Главный",Csv(r).ReadAll(p)[0].Position);}
    [Fact] public void MissingAndEmptyCsvReturnsEmpty(){var r=Temp();Assert.Empty(Csv(r).ReadAll(Path.Combine(r,"missing.csv")));var p=Path.Combine(r,"empty.csv");File.WriteAllText(p,"");Assert.Empty(Csv(r).ReadAll(p));}
    [Fact] public void ReadsHeadersWithSpacesAndDifferentCase(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p," fullName ; position \nИванов;Инженер\n",Encoding.UTF8);Assert.Equal("Иванов",Csv(r).ReadAll(p)[0].FullName);}
    [Fact] public async Task WritesNewEmployeeAndKeepsCyrillic(){var r=Temp();var p=Path.Combine(r,"data/e.csv");var s=Csv(r);s.EnsureDemoCsv(p);var res=await s.AddAsync(p,Path.Combine(r,"backup"),50,new Employee{FullName="  Тестов   Тест  ",Position=" Разработчик "});Assert.True(res.ok);Assert.Contains("Тестов Тест",File.ReadAllText(p,Encoding.UTF8));}
    [Fact] public async Task DetectsDuplicateIgnoringCase(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nИванов Иван;Специалист\n",Encoding.UTF8);var res=await Csv(r).AddAsync(p,Path.Combine(r,"b"),50,new Employee{FullName="иванов иван",Position="Другая"});Assert.Equal("DUPLICATE_EMPLOYEE",res.code);}
    [Fact] public void NormalizesSpaces(){Assert.Equal("А Б В",TextNormalizer.NormalizeSpaces(" А   Б\tВ "));}
    [Fact] public async Task CreatesBackup(){var r=Temp();var p=Path.Combine(r,"e.csv");File.WriteAllText(p,"ФИО;Должность\nА;Б\n",Encoding.UTF8);await Csv(r).AddAsync(p,Path.Combine(r,"backup"),50,new Employee{FullName="В",Position="Г"});Assert.NotEmpty(Directory.GetFiles(Path.Combine(r,"backup"),"employees_*.csv"));}
    [Fact] public void LimitsBackups(){var r=Temp();var b=Path.Combine(r,"backup");Directory.CreateDirectory(b);var svc=new CsvBackupService(new FileLogger(r));for(int i=0;i<5;i++){File.WriteAllText(Path.Combine(b,$"employees_2026-06-30_07300{i}_0000000.csv"),"");Thread.Sleep(5);}svc.Cleanup(b,2);Assert.Equal(2,Directory.GetFiles(b).Length);}
    [Fact] public void RestoresBrokenConfiguration(){var r=Temp();File.WriteAllText(Path.Combine(r,"config.json"),"{");var s=new SettingsService(r,new FileLogger(r));Assert.Equal(5187,s.Current.Server.Port);Assert.NotEmpty(Directory.GetFiles(r,"config.json.broken_*"));}
    [Fact] public void SearchByNameAndPositionAndLimit(){var rows=Enumerable.Range(1,30).Select(i=>new Employee{FullName=$"Иванов {i}",Position=i==2?"Начальник":"Специалист"});var svc=new EmployeeSearchService();Assert.NotEmpty(svc.Search(rows,"иван"));Assert.Single(svc.Search(rows,"началь"));Assert.Equal(20,svc.Search(rows,"спец",20).Count);}
    [Fact] public void SearchNormalizesYoAndSorts(){var rows=new[]{new Employee{FullName="Семёнов Петр",Position="А"},new Employee{FullName="Петр Семёнов",Position="Б"},new Employee{FullName="А",Position="Семёнов"}};var found=new EmployeeSearchService().Search(rows,"семенов");Assert.Equal("Семёнов Петр",found[0].FullName);Assert.Equal("Петр Семёнов",found[1].FullName);Assert.Equal("А",found[2].FullName);}
    [Fact] public void MillimetersConvertToHundredthsOfInch(){Assert.Equal(157, WindowsPrinterService.MmToHundredthsInch(40));Assert.Equal(236, WindowsPrinterService.MmToHundredthsInch(60));}
    [Fact] public async Task ValidatesPrintRequestThroughFake(){IPrinterService p=new FakePrinter();var res=await p.PrintAsync(new Employee{FullName="",Position=""},1,new PrintingSettings());Assert.False(res.ok);}
    private sealed class FakePrinter : IPrinterService { public IReadOnlyList<PrinterInfo> GetPrinters(string s)=>[]; public Task<(bool ok,string message,string code)> PrintAsync(Employee e,int c,PrintingSettings s,CancellationToken t=default)=>Task.FromResult((!string.IsNullOrWhiteSpace(e.FullName)&&!string.IsNullOrWhiteSpace(e.Position),"","VALIDATION_ERROR")); }
}
