using System.Text;

namespace LibVideo.Helpers
{
    /// <summary>
    /// Lightweight pinyin initial extractor using GB2312 encoding ranges.
    /// Zero external dependencies. Converts Chinese characters to their
    /// pinyin first letter (e.g. "战狼" → "ZL").
    /// </summary>
    public static class PinyinHelper
    {
        private static readonly Encoding _gb2312;

        static PinyinHelper()
        {
            try
            {
                _gb2312 = Encoding.GetEncoding("GB2312");
            }
            catch
            {
                _gb2312 = null;
            }
        }

        /// <summary>
        /// Returns a string where each Chinese character is replaced by its
        /// pinyin initial letter. Non-Chinese characters are kept as-is.
        /// Example: "战狼2" → "ZL2"
        /// </summary>
        public static string GetInitials(string text)
        {
            if (string.IsNullOrEmpty(text) || _gb2312 == null) return text ?? "";

            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if (ch >= 0x4E00 && ch <= 0x9FFF)
                {
                    sb.Append(GetChineseInitial(ch));
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static char GetChineseInitial(char ch)
        {
            byte[] bytes;
            try
            {
                bytes = _gb2312.GetBytes(ch.ToString());
            }
            catch
            {
                return ch;
            }

            if (bytes.Length != 2) return ch;

            int code = bytes[0] * 256 + bytes[1];

            if (code < 0xB0A1 || code > 0xD7F9) return ch;

            if (code <= 0xB0C4) return 'A';
            if (code <= 0xB2C0) return 'B';
            if (code <= 0xB4ED) return 'C';
            if (code <= 0xB6E9) return 'D';
            if (code <= 0xB7A1) return 'E';
            if (code <= 0xB8C0) return 'F';
            if (code <= 0xB9FD) return 'G';
            if (code <= 0xBBF6) return 'H';
            if (code <= 0xBFA5) return 'J';
            if (code <= 0xC0AB) return 'K';
            if (code <= 0xC2E7) return 'L';
            if (code <= 0xC4C2) return 'M';
            if (code <= 0xC5B5) return 'N';
            if (code <= 0xC5BD) return 'O';
            if (code <= 0xC6D9) return 'P';
            if (code <= 0xC8BA) return 'Q';
            if (code <= 0xC8F5) return 'R';
            if (code <= 0xCBF9) return 'S';
            if (code <= 0xCDD9) return 'T';
            if (code <= 0xCEF3) return 'W';
            if (code <= 0xD1B8) return 'X';
            if (code <= 0xD4D0) return 'Y';
            if (code <= 0xD7F9) return 'Z';

            return ch;
        }
    }
}
