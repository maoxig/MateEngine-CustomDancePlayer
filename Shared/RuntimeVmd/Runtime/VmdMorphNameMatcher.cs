using System;
using System.Text;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Matches MMD morph names to common Unity/VRM/VRChat BlendShape conventions.
    /// Exact MMD names always win. Alias scores let callers select only the best
    /// convention on a mesh instead of driving duplicate Fcl, VRM and VRC shapes.
    /// </summary>
    public static class VmdMorphNameMatcher
    {
        public const int NoMatch = 0;
        public const int ExactMatch = 10000;
        private static readonly System.Collections.Generic.Dictionary<string,string[]> TranslatedMmd =
            new System.Collections.Generic.Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {"はぅ",new[]{"hachueye","haueye"}},
            {"笑い",new[]{"blinkhappy","eyesmile"}},
            {"ウィンク",new[]{"wink"}}, {"ウィンク2",new[]{"wink2"}},
            {"ウィンク右",new[]{"winkright"}}, {"ウィンク2右",new[]{"wink2right"}},
            {"にこり",new[]{"cheerful"}}, {"真面目",new[]{"serious"}},
            {"困る",new[]{"sadness"}}, {"怒り",new[]{"anger"}},
            {"上",new[]{"upper"}}, {"下",new[]{"lower"}},
            {"ジト目",new[]{"stare","jitome"}}, {"なごみ",new[]{"calm"}},
            {"にやり",new[]{"grin"}}, {"ぺろっ",new[]{"lick"}}
        };

        public static bool IsMatch(string blendShapeName, string vmdMorphName)
        {
            return GetMatchScore(blendShapeName, vmdMorphName) > NoMatch;
        }

        public static int GetMatchScore(string blendShapeName, string vmdMorphName)
        {
            blendShapeName = PresetAlias(blendShapeName);
            string shape = Normalize(blendShapeName);
            string morph = Normalize(PresetAlias(vmdMorphName));
            if (shape.Length == 0 || morph.Length == 0) return NoMatch;

            string unprefixedShape = Normalize(RemovePrefix(blendShapeName));
            if (string.Equals(shape, morph, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unprefixedShape, morph, StringComparison.OrdinalIgnoreCase))
                return ExactMatch;
            string[] translations;
            if(TranslatedMmd.TryGetValue(morph,out translations))
                foreach(string alias in translations)
                    if(Flatten(unprefixedShape)==alias)return 9700;

            MorphSemantic semantic = GetSemantic(morph);
            if (semantic == MorphSemantic.None) return NoMatch;
            if(GetSemantic(unprefixedShape)==semantic)
                foreach(char c in unprefixedShape)if(c>127)return 9500;

            string flatShape = Flatten(shape);
            string flatUnprefixedShape = Flatten(unprefixedShape);
            switch (semantic)
            {
                case MorphSemantic.MouthA:
                    return ScoreMouth(flatShape, flatUnprefixedShape, "a", "aa");
                case MorphSemantic.MouthI:
                    return ScoreMouth(flatShape, flatUnprefixedShape, "i", "ih");
                case MorphSemantic.MouthU:
                    return ScoreMouth(flatShape, flatUnprefixedShape, "u", "ou");
                case MorphSemantic.MouthE:
                    return ScoreMouth(flatShape, flatUnprefixedShape, "e", "ee");
                case MorphSemantic.MouthO:
                    return ScoreMouth(flatShape, flatUnprefixedShape, "o", "oh");
                case MorphSemantic.Blink:
                    return ScoreBlink(flatShape, flatUnprefixedShape);
                case MorphSemantic.BlinkLeft:
                    return ScoreBlinkSide(flatShape, flatUnprefixedShape, true);
                case MorphSemantic.BlinkRight:
                    return ScoreBlinkSide(flatShape, flatUnprefixedShape, false);
                case MorphSemantic.Joy:
                    return ScoreExpression(flatShape, flatUnprefixedShape, "joy", "happy", "smile");
                case MorphSemantic.Fun:
                    return ScoreExpression(flatShape, flatUnprefixedShape, "fun", "relaxed", "relax");
                case MorphSemantic.Angry:
                    return ScoreExpression(flatShape, flatUnprefixedShape, "angry", "anger");
                case MorphSemantic.Sorrow:
                    return ScoreExpression(flatShape, flatUnprefixedShape, "sorrow", "sad", "worried", "worry");
                case MorphSemantic.Surprised:
                    return ScoreExpression(flatShape, flatUnprefixedShape, "surprised", "surprise");
                default:
                    return NoMatch;
            }
        }

        private static string PresetAlias(string name)
        {
            switch((name??"").ToLowerInvariant())
            {
                case "aa":return "A";case "ih":return "I";case "ou":return "U";case "ee":return "E";case "oh":return "O";
                case "blink_l":return "blinkLeft";case "blink_r":return "blinkRight";
                default:return name;
            }
        }

        private static int ScoreMouth(string shape, string unprefixed, string vowel, string vrcViseme)
        {
            if (EndsWith(shape, "fclmth" + vowel)) return 900;
            if (EndsWith(shape, "mouthbs" + "m" + vowel) || unprefixed == "m" + vowel) return 870;
            if (EndsWith(shape, "mouth" + vrcViseme)) return 850;
            if (vrcViseme != vowel && EndsWith(shape, "mouth" + vowel)) return 840;
            if (EndsWith(shape, "viseme" + vrcViseme) || EndsWith(shape, "vrcv" + vrcViseme)) return 820;
            if (unprefixed == "mouth" + vowel || unprefixed == "mouth" + vrcViseme) return 800;
            if (unprefixed == vowel || unprefixed == vrcViseme) return 700;
            return NoMatch;
        }

        private static int ScoreBlink(string shape, string unprefixed)
        {
            if (EndsWith(shape, "fcleyeclose") || unprefixed == "close") return 920;
            if (EndsWith(shape, "eyelidblink") || EndsWith(shape, "vrcblink") ||
                unprefixed == "blink" || unprefixed == "eyeblink" || unprefixed == "eyeclose") return 900;

            // If a model has no combined blink, the equal scores deliberately
            // select both left and right shapes.
            int left = ScoreBlinkSide(shape, unprefixed, true);
            int right = ScoreBlinkSide(shape, unprefixed, false);
            return Math.Max(left, right) > NoMatch ? 860 : NoMatch;
        }

        private static int ScoreBlinkSide(string shape, string unprefixed, bool left)
        {
            string sideLong = left ? "left" : "right";
            string sideShort = left ? "l" : "r";
            if (EndsWith(shape, "fcleyeclose" + sideShort)) return 900;
            if (EndsWith(shape, "eyeblink" + sideLong) || EndsWith(shape, "eyelidblink" + sideShort) ||
                EndsWith(shape, "blink" + sideLong) || EndsWith(shape, "blink" + sideShort) ||
                EndsWith(shape, "eyeclose" + sideShort) || EndsWith(shape, "eyeclose" + sideLong)) return 880;
            if (unprefixed == "wink" + sideShort || unprefixed == "wink" + sideLong ||
                unprefixed == "close" + sideShort || unprefixed == "close" + sideLong) return 820;
            return NoMatch;
        }

        private static int ScoreExpression(string shape, string unprefixed, params string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                if (EndsWith(shape, "fclall" + name)) return 900 - index;
            }
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                if (EndsWith(shape, "expression" + name) || EndsWith(shape, "vrm" + name) ||
                    unprefixed == name) return 840 - index;
            }
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                if (EndsWith(shape, "fcleye" + name) || EndsWith(shape, "eye" + name)) return 780 - index;
            }
            return NoMatch;
        }

        private static MorphSemantic GetSemantic(string normalizedMorphName)
        {
            string value = Flatten(normalizedMorphName);
            switch (value)
            {
                case "あ":
                case "a":
                case "aa":
                    return MorphSemantic.MouthA;
                case "い":
                case "i":
                case "ih":
                    return MorphSemantic.MouthI;
                case "う":
                case "u":
                case "ou":
                    return MorphSemantic.MouthU;
                case "え":
                case "e":
                case "ee":
                    return MorphSemantic.MouthE;
                case "お":
                case "o":
                case "oh":
                    return MorphSemantic.MouthO;
                case "まばたき":
                case "瞬き":
                case "blink":
                    return MorphSemantic.Blink;
                case "ウィンク":
                case "ウィンク2":
                case "wink":
                case "winkleft":
                    return MorphSemantic.BlinkLeft;
                case "ウィンク右":
                case "ウィンク2右":
                case "winkright":
                    return MorphSemantic.BlinkRight;
                case "笑い":
                case "笑顔":
                case "joy":
                case "happy":
                    return MorphSemantic.Joy;
                case "にこり":
                case "fun":
                case "relaxed":
                    return MorphSemantic.Fun;
                case "怒り":
                case "angry":
                    return MorphSemantic.Angry;
                case "困る":
                case "悲しい":
                case "sorrow":
                case "sad":
                    return MorphSemantic.Sorrow;
                case "びっくり":
                case "驚き":
                case "surprised":
                case "surprise":
                    return MorphSemantic.Surprised;
                default:
                    return MorphSemantic.None;
            }
        }

        private static string Normalize(string value)
        {
            return VmdMotionSampler.NormalizeMmdName(value);
        }

        private static string RemovePrefix(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int separator = -1;
            separator = Math.Max(separator, value.LastIndexOf('.'));
            separator = Math.Max(separator, value.LastIndexOf('/'));
            separator = Math.Max(separator, value.LastIndexOf('\\'));
            separator = Math.Max(separator, value.LastIndexOf(':'));
            separator = Math.Max(separator, value.LastIndexOf('|'));
            return separator >= 0 ? value.Substring(separator + 1) : value;
        }

        private static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder result = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character)) result.Append(char.ToLowerInvariant(character));
            }
            return result.ToString();
        }

        private static bool EndsWith(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.Ordinal);
        }

        private enum MorphSemantic
        {
            None,
            MouthA,
            MouthI,
            MouthU,
            MouthE,
            MouthO,
            Blink,
            BlinkLeft,
            BlinkRight,
            Joy,
            Fun,
            Angry,
            Sorrow,
            Surprised
        }
    }
}
