using System;
using System.Threading.Tasks;

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
                // TODO: Load configuration (cert path, password, endpoints)
                var config = new AppConfig
                {
                    CertificatePath = "path/to/your/certificate.pfx",
                    CertificatePassword = "your_password",
                    SubmissionEndpoint = "https://la.www4.irs.gov/airp/aca/a2a/1095BC_Transmission",
                    StatusEndpoint = "https://la.www4.irs.gov/airp/aca/a2a/1095BC_Status_Request"
                };

                Console.WriteLine("Transmitting test submission...");

                var transmitter = new AcaTransmitterService(config);
                var response = await transmitter.TransmitAsync("path/to/formdata.xml", "path/to/manifest.xml");

                Console.WriteLine("\nResponse from server:");
                Console.WriteLine(response);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
        }
    }
}
