using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONConfigReader
    {
        public JSONConfig config;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/config.json"))
            {
                string json = await sr.ReadToEndAsync();
                JSONConfig data = JsonConvert.DeserializeObject<JSONConfig>(json);

                config = data;
            }
        }
    }

    internal class JSONConfig
    {
        public string token;
        public string prefix;
        public ulong ID_guild;
        public ulong ID_generalChannel;
        public ulong ID_staffChannel;
        public ulong ID_annivChannel;
        public string URL_annivPicture;
        public ulong ID_newsFeedChannel;
        public ulong ID_CustomVoiceChannel;
    }
}
