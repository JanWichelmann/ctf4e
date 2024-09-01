using System.Text.Json;
using Ctf4e.Api.Services;

namespace Ctf4e.Api
{
    public class CtfApiRequest
    {
        public int LabId { get; set; }

        public string Data { get; set; }

        public static CtfApiRequest Create<T>(int labId, ICryptoService cryptoService, T data) where T : class
        {
            return new CtfApiRequest
            {
                LabId = labId,
                Data = cryptoService.Encrypt(JsonSerializer.Serialize(data))
            };
        }

        public T Decode<T>(ICryptoService cryptoService) where T : class
        {
            return JsonSerializer.Deserialize<T>(cryptoService.Decrypt(Data));
        }
    }
}