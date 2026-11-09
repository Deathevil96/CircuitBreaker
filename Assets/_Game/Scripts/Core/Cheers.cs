using System;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Encouraging words shown when a board is cleared, in many languages. Scripts the game's text renderer cannot
    /// shape (Arabic, Hebrew, Hindi, Thai) are written in Latin letters so they never show as boxes or reversed.
    /// </summary>
    public static class Cheers
    {
        public static readonly string[] All =
        {
            // English
            "Brilliant!", "Amazing!", "Nailed it!", "You're on fire!", "Superb!", "Outstanding!",
            // Spanish, Portuguese, Italian, French
            "¡Genial!", "¡Excelente!", "¡Increíble!", "Incrível!", "Mandou bem!", "Fantastico!", "Bravissimo!", "Magnifique !", "Bravo !", "Formidable !",
            // German, Dutch, Swedish, Finnish, Polish, Czech, Romanian, Hungarian
            "Großartig!", "Spitze!", "Geweldig!", "Fantastiskt!", "Mahtavaa!", "Świetnie!", "Skvělé!", "Excelent!", "Szuper!",
            // Turkish, Indonesian, Filipino, Swahili, Vietnamese, Welsh, Irish
            "Harika!", "Aferin!", "Keren!", "Galing!", "Hongera!", "Tuyệt vời!", "Gwych!", "Iontach!",
            // Hindi, Arabic, Hebrew, Thai (Latin letters)
            "Shabash!", "Kamaal!", "Mumtaz!", "Kol hakavod!", "Yoot yiam!",
            // Japanese, Chinese, Korean
            "すごい!", "素晴らしい!", "太棒了!", "真厉害!", "대단해요!", "최고예요!",
            // Russian, Ukrainian, Greek
            "Отлично!", "Молодец!", "Чудово!", "Μπράβο!", "Τέλεια!",
        };

        private static readonly Random Rng = new Random();
        private static int _last = -1;

        /// <summary>A random cheer, never the same one twice in a row.</summary>
        public static string Pick()
        {
            int i;
            do i = Rng.Next(All.Length); while (i == _last && All.Length > 1);
            _last = i;
            return All[i];
        }
    }
}
