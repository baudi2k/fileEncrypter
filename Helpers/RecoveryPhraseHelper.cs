using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FileEncrypter.Helpers
{
    public static class RecoveryPhraseHelper
    {
        // Lista de palabras en español para las frases mnemónicas (BIP39 simplificado)
        private static readonly string[] WordList = new string[]
        {
            "abedul", "abismo", "abolir", "abrazo", "abrir", "abuelo", "acabar", "acento", "aceptar", "ácido",
            "aclarar", "acné", "acoger", "acoso", "actuar", "acudir", "acuerdo", "acusar", "adicto", "admitir",
            "adoptar", "adorno", "aduana", "adulto", "aéreo", "afecto", "afilar", "afirmar", "ágil", "agitar",
            "agonía", "agosto", "agotar", "agregar", "agrio", "agua", "agudo", "águila", "aguja", "ahogo",
            "ahorro", "aire", "aislar", "ajedrez", "ajeno", "ajuste", "alacrán", "alambre", "alarma", "alba",
            "álbum", "alcalde", "aldea", "alegre", "alejar", "alerta", "aleta", "alfiler", "alga", "algodón",
            "aliado", "aliento", "alivio", "alma", "almeja", "almíbar", "altar", "alteza", "altivo", "alto",
            "altura", "alumno", "alzar", "amable", "amante", "amasar", "ámbito", "amenaza", "amigo", "amistad",
            "amor", "amparo", "amplio", "ancho", "anciano", "ancla", "andar", "andén", "anemia", "ángulo",
            "anillo", "ánimo", "anís", "anotar", "antena", "antiguo", "antojo", "anual", "anular", "anuncio",
            "añadir", "añejo", "año", "apagar", "aparato", "apetito", "apio", "aplicar", "apodo", "aporte",
            "apostar", "apoyo", "aprender", "aprobar", "apuesta", "apuro", "arado", "araña", "arar", "árbitro",
            "árbol", "arbusto", "archivo", "arco", "arder", "ardilla", "arduo", "área", "árido", "arma",
            "armonía", "aroma", "arpa", "arpón", "arreglo", "arroz", "arruga", "arte", "artista", "asa",
            "asado", "asalto", "ascenso", "asegurar", "aseo", "asesor", "asiento", "asilo", "asistir", "asno",
            "asombro", "áspero", "astilla", "astro", "astuto", "asumir", "asunto", "atajo", "ataque", "atar",
            "atento", "ateo", "ático", "atleta", "átomo", "atraer", "atroz", "atún", "audaz", "audio",
            "auge", "aula", "aumento", "ausente", "autor", "aval", "avance", "avaro", "ave", "avellana",
            "avena", "avestruz", "avión", "aviso", "ayer", "ayuda", "ayuno", "azahar", "azar", "azote",
            "azúcar", "azufre", "azul", "baba", "babor", "bache", "bahía", "baile", "bajar", "balanza",
            "balcón", "balde", "bambú", "banco", "banda", "baño", "barba", "barco", "barniz", "barro",
            "báscula", "bastón", "basura", "batalla", "batería", "batir", "batuta", "baúl", "bazar", "bebé",
            "bebida", "bello", "besar", "beso", "bestia", "bicho", "bien", "bingo", "blanco", "bloque",
            "bobina", "bobo", "boca", "bocina", "boda", "bodega", "boina", "bola", "bolero", "bolsa",
            "bomba", "bondad", "bonito", "borde", "borrar", "bosque", "bote", "botín", "bóveda", "boxeo",
            "bravo", "brazo", "brecha", "breve", "brillo", "brinco", "brisa", "broca", "broma", "bronce",
            "brote", "bruja", "brusco", "bruto", "buceo", "bucle", "bueno", "buey", "bufanda", "bufón",
            "búho", "buitre", "bulto", "burbuja", "burla", "burro", "buscar", "butaca", "buzón", "caballo"
        };

        public static RecoveryPhraseResult GenerateRecoveryPhrase()
        {
            try
            {
                var words = new List<string>();
                using (var rng = RandomNumberGenerator.Create())
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var bytes = new byte[4];
                        rng.GetBytes(bytes);
                        var index = Math.Abs(BitConverter.ToInt32(bytes, 0)) % WordList.Length;
                        words.Add(WordList[index]);
                    }
                }

                var phrase = string.Join(" ", words);
                var key = DeriveKeyFromPhrase(phrase);

                return new RecoveryPhraseResult
                {
                    Phrase = phrase,
                    DerivedKey = Convert.ToBase64String(key),
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new RecoveryPhraseResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public static bool ValidateRecoveryPhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return false;

            var words = phrase.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length != 12)
                return false;

            return words.All(word => WordList.Contains(word));
        }

        public static string DeriveKeyFromPhraseAsBase64(string phrase)
        {
            try
            {
                var key = DeriveKeyFromPhrase(phrase);
                return Convert.ToBase64String(key);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static byte[] DeriveKeyFromPhrase(string phrase)
        {
            // Usar PBKDF2 para derivar una clave criptográficamente segura desde la frase
            var phraseBytes = Encoding.UTF8.GetBytes(phrase);
            var salt = Encoding.UTF8.GetBytes("FileEncrypterRecovery2024"); // Salt fijo para consistencia
            
            using (var pbkdf2 = new Rfc2898DeriveBytes(phraseBytes, salt, 100000, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32); // 256 bits
            }
        }

        public static List<string> GetPhraseWords(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return new List<string>();

            return phrase.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static string FormatPhraseForDisplay(string phrase)
        {
            var words = GetPhraseWords(phrase);
            if (words.Count != 12)
                return phrase;

            var formatted = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
            {
                formatted.Append($"{i + 1,2}. {words[i]}");
                if (i < words.Count - 1)
                {
                    if ((i + 1) % 4 == 0)
                        formatted.AppendLine();
                    else
                        formatted.Append("   ");
                }
            }
            return formatted.ToString();
        }
    }

    public class RecoveryPhraseResult
    {
        public string Phrase { get; set; } = "";
        public string DerivedKey { get; set; } = "";
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
} 