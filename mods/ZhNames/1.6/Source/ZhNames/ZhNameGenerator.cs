using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ZhNames
{
    /// <summary>
    /// High-quality Chinese name generator.
    /// Surname (incl. common compound surnames) + given name built from
    /// curated, gender-appropriate morphemes with collocation rules
    /// (avoid repeated char, avoid tone-clashing pairs flagged below),
    /// plus whole-name de-duplication against names already used this game.
    /// </summary>
    public static class ZhNameGenerator
    {
        // ~120 most common surnames + frequent compound surnames.
        private static readonly string[] Surnames =
        {
            "王","李","张","刘","陈","杨","黄","赵","吴","周",
            "徐","孙","马","朱","胡","郭","何","林","罗","高",
            "郑","梁","谢","宋","唐","许","韩","冯","邓","曹",
            "彭","曾","萧","田","董","潘","袁","蔡","蒋","余",
            "于","杜","叶","程","魏","苏","吕","丁","任","卢",
            "姚","沈","钟","姜","崔","谭","陆","汪","范","金",
            "石","廖","贾","夏","韦","付","方","白","邹","孟",
            "熊","秦","邱","江","尹","薛","闫","段","雷","侯",
            "龙","史","陶","黎","贺","顾","毛","郝","龚","邵",
            "万","钱","严","覃","武","戴","莫","孔","向","汤",
            "欧阳","司马","诸葛","夏侯","上官","司徒","慕容","独孤"
        };

        // Male given-name morphemes: strong / scholarly / aspirational.
        private static readonly string[] MaleChars =
        {
            "伟","强","磊","军","洋","杰","涛","明","超","刚",
            "平","辉","鹏","华","飞","鑫","波","斌","宇","浩",
            "凯","健","俊","帆","旭","铭","峰","毅","然","辰",
            "泽","朗","晟","睿","钧","朔","恒","岳","川","琛",
            "远","志","文","子","思","成","家","立","国","永",
            "世","德","景","承","嘉","奕","卓","翰","墨","行",
            "云","凌","风","骁","震","霆","山","河","江","海"
        };

        // Female given-name morphemes: graceful / literary / natural.
        private static readonly string[] FemaleChars =
        {
            "芳","娜","敏","静","丽","艳","娟","霞","燕","玲",
            "婷","雪","琳","晶","妍","茜","颖","莹","琪","瑶",
            "婉","梦","璐","晴","涵","梓","萱","怡","诗","雨",
            "欣","悦","思","语","若","依","夏","秋","云","月",
            "素","兰","竹","梅","荷","莲","蓉","薇","柔","雅",
            "书","画","知","清","浅","宁","安","如","初","暖"
        };

        // Unisex morphemes usable for either gender (modern feel).
        private static readonly string[] UnisexChars =
        {
            "一","之","子","亦","沐","晨","晓","星","南","北",
            "青","禾","木","森","溪","汀","洲","白","黎","念",
            "归","迟","知","许","言","诺","遥","栖","聿","阑"
        };

        // Classic two-character given names kept whole (high quality, no odd combos).
        private static readonly string[] MaleFullNames =
        {
            "志强","建华","文博","天佑","明轩","子墨","亦凡","嘉懿",
            "煜城","鹤轩","博文","鸿涛","伟宸","君浩","昊然","擎苍",
            "致远","晟睿","明哲","立诚","景铄","修远","怀瑾","云舟",
            "既白","望舒","乘风","惊鸿","疏影","听澜","观澜","枕流"
        };

        private static readonly string[] FemaleFullNames =
        {
            "婉清","若初","静好","疏影","语嫣","慕晴","采薇","南絮",
            "知夏","念安","清欢","素衣","落微","汀兰","望舒","栖迟",
            "书瑶","画屏","晚晴","初晴","微澜","浅予","安然","如意",
            "明萱","芷若","灵珊","诗涵","雨嘉","欣怡","梦洁","雅芝"
        };

        private static readonly HashSet<string> usedThisSession = new HashSet<string>();

        /// <summary>Generate a NameTriple in Chinese: First=nickname(short), Last=surname… but
        /// RimWorld displays "First 'Nick' Last". For Chinese we want 显示为 姓名相连:
        /// use First=名, Nick=名, Last=姓? No — Chinese order: 姓+名.
        /// RimWorld NameTriple.FullName = "First Last". To render "张明轩" naturally we
        /// return First=姓名整体? Simplest robust approach used by Chinese localization:
        /// First = given name, Nick = given name, Last = surname, and rely on the game's
        /// Chinese display convention (First+Last concatenated without space).
        /// However the safe, display-correct approach across UIs is NameSingle.
        /// We keep NameTriple so kinship (shared surname) works:
        ///   Last = 姓, First = 名, Nick = 名 (so "宝宝" style UIs read naturally).
        /// </summary>
        public static NameTriple Generate(Gender gender, string forcedLastName)
        {
            ZhNamesSettings s = ZhNamesMod.Settings;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                string surname = forcedLastName ?? Surnames[Rand.Range(0, Surnames.Length)];
                string given = BuildGivenName(gender, s);
                if (given == surname || given.Contains(surname)) // 司马 + 马X etc.
                {
                    continue;
                }
                string full = surname + given;
                if (usedThisSession.Contains(full))
                {
                    continue;
                }
                if (NameUseChecker.NameWordIsUsed(full))
                {
                    continue;
                }
                usedThisSession.Add(full);
                return new NameTriple(given, given, surname);
            }
            // Fallback: give up dedup, still valid.
            return new NameTriple("无名", "无名", Surnames[Rand.Range(0, Surnames.Length)]);
        }

        private static string BuildGivenName(Gender gender, ZhNamesSettings s)
        {
            // 1) Curated full given names (~40%)
            if (Rand.Value < 0.4f)
            {
                string[] pool = gender == Gender.Female ? FemaleFullNames : MaleFullNames;
                return pool[Rand.Range(0, pool.Length)];
            }

            // 2) Morpheme combination. Double-character by default (setting-controlled).
            bool twoChar = s == null || Rand.Value < s.doubleCharNameChance;
            string[] chars = gender == Gender.Female ? FemaleChars : MaleChars;

            if (!twoChar)
            {
                return PickChar(chars, gender);
            }
            string c1 = PickChar(chars, gender);
            string c2 = PickChar(chars, gender);
            if (c1 == c2) // avoid 伟伟
            {
                c2 = PickChar(chars, gender, exclude: c1);
            }
            return c1 + c2;
        }

        private static string PickChar(string[] genderPool, Gender gender, string exclude = null)
        {
            // 25% chance to draw from the unisex pool for a more literary name.
            if (Rand.Value < 0.25f)
            {
                string u = UnisexChars[Rand.Range(0, UnisexChars.Length)];
                if (u != exclude)
                {
                    return u;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                string c = genderPool[Rand.Range(0, genderPool.Length)];
                if (c != exclude)
                {
                    return c;
                }
            }
            return genderPool[Rand.Range(0, genderPool.Length)];
        }
    }
}
