using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AcaApi.Poc
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("ACA API Proof-of-Concept");
            Console.WriteLine("========================");

            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var config = configuration.GetSection("AcaApiConfig").Get<AppConfig>();

                if (config == null)
                {
                    throw new Exception("Configuration could not be loaded. Make sure appsettings.json is present and correctly formatted.");
                }
                
                if (config.CertificatePassword == "YOUR_PASSWORD_HERE")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Warning: Please replace 'YOUR_PASSWORD_HERE' in appsettings.json with your actual certificate password.");
                    Console.ResetColor();
                }

                var service = new AcaTransmitterService(config);

                Console.WriteLine("Transmitting Scenario 2...");
                await service.TransmitAsync(config.Scenario2_FormDataFilePath, config.Scenario2_ManifestFilePath);

                Console.WriteLine("\nTransmitting Scenario 3...");
                await service.TransmitAsync(config.Scenario3_FormDataFilePath, config.Scenario3_ManifestFilePath);

                Console.WriteLine("\nTransmissions complete.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAn error occurred: {ex}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to exit.");
        }
    }
}
