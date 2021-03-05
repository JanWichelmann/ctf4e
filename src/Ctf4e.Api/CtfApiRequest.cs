using Ctf4e.Api.Services;
using Newtonsoft.Json;

namespace Ctf4e.Api
{
    public class CtfApiRequest
    {
        public int LessonId { get; set; }

        public string Data { get; set; }

        public CtfApiRequest()
        {
        }

        public static CtfApiRequest Create<T>(int lessonId, ICryptoService cryptoService, T data) where T : class
        {
            return new CtfApiRequest
            {
                LessonId = lessonId,
                Data = cryptoService.Encrypt(JsonConvert.SerializeObject(data))
            };
        }

        public T Decode<T>(ICryptoService cryptoService) where T : class
        {
            return JsonConvert.DeserializeObject<T>(cryptoService.Decrypt(Data));
        }
    }
}