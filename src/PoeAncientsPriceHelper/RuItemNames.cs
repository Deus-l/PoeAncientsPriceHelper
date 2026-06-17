namespace PoeAncientsPriceHelper;

// RU (русский клиент PoE2) -> EN (каноническое имя, как оно приходит из poe.ninja API)
// сопоставление названий предметов, плюс резолвер для использования в ScanEngine.
//
// Источник данных: poe2db.tw/ru — этот сайт тянет строки напрямую из файлов локализации
// игры, а не переводит "по смыслу", так что это реальные строки, которые показывает клиент.
// Проверено вживую на разделе Stackable_Currency (https://poe2db.tw/ru/Stackable_Currency),
// который покрывает категории Currency, Verisium и часть Expedition из
// PriceRepository.ExchangeTypes.
//
// ЧЕСТНО НЕ ПОКРЫТО на этом проходе (см. ниже, не выдумано):
//   - Базовые сокетные Runes из лиги "Руны Альдура" (PriceRepository.ExchangeTypes содержит
//     "Runes") — известно, что их 35 (22 защитных + 13 для оружия), но точные официальные RU
//     строки для каждой не подтверждены через poe2db.tw/ru за один проход. Лучше оставить
//     категорию пустой, чем подсунуть придуманные названия, которые тихо сломают сопоставление.
//   - UncutGems (Uncut Skill/Spirit/Support Gem) — две независимые публичные RU-инструкции
//     (lootkeeper.com, naveselegaming.ru) сходятся на одном переводе, но он не подтверждён через
//     официальный источник локализации. Сверь с реальным клиентом перед тем как доверять
//     точному совпадению по этим трём строкам.
//   - "Ancient Liquid Paranoia" сознательно пропущен: на странице poe2db.tw/ru у него та же RU-
//     строка, что и у обычного "Liquid Paranoia" ("Жидкая паранойя") — похоже на баг недо-
//     переведённой строки на сайте. Включать его значило бы либо дублировать ключ словаря
//     (ошибка компиляции), либо тихо перезаписать корректную пару — оставил как пропуск, а не
//     угадал собственный вариант перевода.
//
// Полный список 143 пунктов раздела "Валюта" с вики не выкачан целиком: страница подгружается
// по частям через JS, инструмент фетча отдаёт первые ~110 пунктов и обрывается на одном и том же
// месте при повторном заходе. То, что ниже — все пункты, которые удалось получить, без дырок
// внутри этого диапазона.
internal static class RuItemNames
{
    // Хранится как обычный текст (с заглавными буквами, апострофами и т.п.) — нормализацию
    // (lowercase, отсечение пунктуации) делает BuildNormalized() через OcrScanner.NormalizeName,
    // ту же функцию, что причёсывает реальный OCR-вывод. Так пары гарантированно остаются в
    // одном формате с тем, что реально приходит со сканера и из PriceRepository.
    public static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- Currency (подтверждено poe2db.tw/ru) ---
        ["Точильный камень"] = "Blacksmith's Whetstone",
        ["Резец чародея"] = "Arcanist's Etcher",
        ["Свиток мудрости"] = "Scroll of Wisdom",
        ["Сфера хаоса"] = "Chaos Orb",
        ["Большая сфера хаоса"] = "Greater Chaos Orb",
        ["Совершенная сфера хаоса"] = "Perfect Chaos Orb",
        ["Деталь доспеха"] = "Armourer's Scrap",
        ["Зеркало Каландры"] = "Mirror of Kalandra",
        ["Прядь Хинекоры"] = "Hinekora's Lock",
        ["Сфера алхимии"] = "Orb of Alchemy",
        ["Сфера удачи"] = "Orb of Chance",
        ["Сфера превращения"] = "Orb of Transmutation",
        ["Большая сфера превращения"] = "Greater Orb of Transmutation",
        ["Совершенная сфера превращения"] = "Perfect Orb of Transmutation",
        ["Сфера возвышения"] = "Exalted Orb",
        ["Большая сфера возвышения"] = "Greater Exalted Orb",
        ["Совершенная сфера возвышения"] = "Perfect Exalted Orb",
        ["Сфера царей"] = "Regal Orb",
        ["Большая сфера царей"] = "Greater Regal Orb",
        ["Совершенная сфера царей"] = "Perfect Regal Orb",
        ["Сфера усиления"] = "Orb of Augmentation",
        ["Большая сфера усиления"] = "Greater Orb of Augmentation",
        ["Совершенная сфера усиления"] = "Perfect Orb of Augmentation",
        ["Стекольная масса"] = "Glassblower's Bauble",
        ["Призма камнереза"] = "Gemcutter's Prism",
        ["Малая сфера златокузнеца"] = "Lesser Jeweller's Orb",
        ["Большая сфера златокузнеца"] = "Greater Jeweller's Orb",
        ["Совершенная сфера златокузнеца"] = "Perfect Jeweller's Orb",
        ["Сфера астромантии"] = "Artificer's Orb",
        ["Божественная сфера"] = "Divine Orb",
        ["Осколок превращения"] = "Transmutation Shard",
        ["Осколок удачи"] = "Chance Shard",
        ["Перо белого роа"] = "Albino Rhoa Feather",
        ["Сфера ваал"] = "Vaal Orb",
        ["Сфера архитектора"] = "Architect's Orb",
        ["Кристаллизованная Порча"] = "Crystallised Corruption",
        ["Дестабилизатор ядра"] = "Core Destabiliser",
        ["Древний нагнетатель"] = "Ancient Infuser",
        ["Культивирующая сфера ваал"] = "Vaal Cultivation Orb",
        ["Нагнетатель бронника ваал"] = "Vaal Armourer's Infuser",
        ["Нагнетатель кузнеца ваал"] = "Vaal Blacksmith's Infuser",
        ["Нагнетатель чародея ваал"] = "Vaal Arcanist's Infuser",
        ["Катализирующий нагнетатель ваал"] = "Vaal Catalysing Infuser",
        ["Сфера извлечения"] = "Orb of Extraction",
        ["Сфера отмены"] = "Orb of Annulment",
        ["Раскалывающая сфера"] = "Fracturing Orb",
        ["Осколок царей"] = "Regal Shard",
        ["Осколок астромантии"] = "Artificer's Shard",
        ["Диковинные монеты"] = "Exotic Coinage",
        ["Золото"] = "Gold",
        ["Скрытый ключ"] = "Cryptic Key",
        ["Поглотитель ваал"] = "Vaal Siphoner",

        // --- Runes of Aldur / Runes (poe.ninja Runes, RU строки подтверждены poe2db.tw/ru) ---
        ["Руна искусности"] = "Adept Rune",
        ["Малая руна искусности"] = "Lesser Adept Rune",
        ["Большая руна искусности"] = "Greater Adept Rune",
        ["Безупречная руна искусности"] = "Perfect Adept Rune",
        ["Руна тела"] = "Body Rune",
        ["Малая руна тела"] = "Lesser Body Rune",
        ["Большая руна тела"] = "Greater Body Rune",
        ["Безупречная руна тела"] = "Perfect Body Rune",
        ["Руна заряда"] = "Charging Rune",
        ["Большая руна заряда"] = "Greater Charging Rune",
        ["Безупречная руна заряда"] = "Perfect Charging Rune",
        ["Руна пустыни"] = "Desert Rune",
        ["Малая руна пустыни"] = "Lesser Desert Rune",
        ["Большая руна пустыни"] = "Greater Desert Rune",
        ["Безупречная руна пустыни"] = "Perfect Desert Rune",
        ["Руна ледника"] = "Glacial Rune",
        ["Малая руна ледника"] = "Lesser Glacial Rune",
        ["Большая руна ледника"] = "Greater Glacial Rune",
        ["Безупречная руна ледника"] = "Perfect Glacial Rune",
        ["Руна вдохновения"] = "Inspiration Rune",
        ["Малая руна вдохновения"] = "Lesser Inspiration Rune",
        ["Большая руна вдохновения"] = "Greater Inspiration Rune",
        ["Безупречная руна вдохновения"] = "Perfect Inspiration Rune",
        ["Руна железа"] = "Iron Rune",
        ["Малая руна железа"] = "Lesser Iron Rune",
        ["Большая руна железа"] = "Greater Iron Rune",
        ["Безупречная руна железа"] = "Perfect Iron Rune",
        ["Руна разума"] = "Mind Rune",
        ["Малая руна разума"] = "Lesser Mind Rune",
        ["Большая руна разума"] = "Greater Mind Rune",
        ["Безупречная руна разума"] = "Perfect Mind Rune",
        ["Руна перерождения"] = "Rebirth Rune",
        ["Малая руна перерождения"] = "Lesser Rebirth Rune",
        ["Большая руна перерождения"] = "Greater Rebirth Rune",
        ["Безупречная руна перерождения"] = "Perfect Rebirth Rune",
        ["Руна решительности"] = "Resolve Rune",
        ["Малая руна решительности"] = "Lesser Resolve Rune",
        ["Большая руна решительности"] = "Greater Resolve Rune",
        ["Безупречная руна решительности"] = "Perfect Resolve Rune",
        ["Руна мощи"] = "Robust Rune",
        ["Малая руна мощи"] = "Lesser Robust Rune",
        ["Большая руна мощи"] = "Greater Robust Rune",
        ["Безупречная руна мощи"] = "Perfect Robust Rune",
        ["Руна камня"] = "Stone Rune",
        ["Малая руна камня"] = "Lesser Stone Rune",
        ["Большая руна камня"] = "Greater Stone Rune",
        ["Безупречная руна камня"] = "Perfect Stone Rune",
        ["Руна шторма"] = "Storm Rune",
        ["Малая руна шторма"] = "Lesser Storm Rune",
        ["Большая руна шторма"] = "Greater Storm Rune",
        ["Безупречная руна шторма"] = "Perfect Storm Rune",
        ["Руна видения"] = "Vision Rune",
        ["Малая руна видения"] = "Lesser Vision Rune",
        ["Большая руна видения"] = "Greater Vision Rune",
        ["Безупречная руна видения"] = "Perfect Vision Rune",
        ["Руна барьера"] = "Ward Rune",
        ["Малая руна барьера"] = "Lesser Ward Rune",
        ["Большая руна барьера"] = "Greater Ward Rune",
        ["Безупречная руна барьера"] = "Perfect Ward Rune",
        ["Мастерская руна"] = "Masterwork Rune",
        ["Руна накопления"] = "Rune of Accumulation",
        ["Руна акробатики"] = "Rune of Acrobatics",
        ["Руна противоборства"] = "Rune of Confrontation",
        ["Руна постоянства"] = "Rune of Consistency",
        ["Руна кульминации"] = "Rune of Culmination",
        ["Руна основ"] = "Rune of Foundations",
        ["Руна охвата"] = "Rune of Reach",
        ["Руна славы"] = "Rune of Renown",
        ["Руна цветения"] = "Rune of the Blossom",
        ["Руна охоты"] = "Rune of the Hunt",
        ["Руна призмы"] = "Rune of the Prism",
        ["Руна живого пламени"] = "Rune of Vital Flame",
        ["Руна живучести"] = "Rune of Vitality",
        ["Большая руна стремления"] = "Greater Rune of Alacrity",
        ["Большая руна лидерства"] = "Greater Rune of Leadership",
        ["Большая руна дворянства"] = "Greater Rune of Nobility",
        ["Большая руна десятины"] = "Greater Rune of Tithing",
        ["Древняя руна вражды"] = "Ancient Rune of Animosity",
        ["Древняя руна правления"] = "Ancient Rune of Control",
        ["Древняя руна тлена"] = "Ancient Rune of Decay",
        ["Древняя руна подрыва"] = "Ancient Rune of Detonation",
        ["Древняя руна находки"] = "Ancient Rune of Discovery",
        ["Древняя руна поединка"] = "Ancient Rune of Dueling",
        ["Древняя руна мастерства"] = "Ancient Rune of Prowess",
        ["Древняя руна расплаты"] = "Ancient Rune of Retaliation",
        ["Древняя руна разбивания"] = "Ancient Rune of Shattering",
        ["Древняя руна осколков"] = "Ancient Rune of Splinters",
        ["Древняя руна орды"] = "Ancient Rune of the Horde",
        ["Древняя руна Титана"] = "Ancient Rune of the Titan",
        ["Древняя руна ведьмовства"] = "Ancient Rune of Witchcraft",
        ["Барьерная руна уничтожения"] = "Warding Rune of Annihilation",
        ["Барьерная руна панциря"] = "Warding Rune of Armature",
        ["Барьерная руна телохранителей"] = "Warding Rune of Bodyguards",
        ["Барьерная руна храбрости"] = "Warding Rune of Courage",
        ["Барьерная руна отчаяния"] = "Warding Rune of Desperation",
        ["Барьерная руна расщепления"] = "Warding Rune of Disintegration",
        ["Барьерная руна равноденствия"] = "Warding Rune of Equinox",
        ["Барьерная руна скольжения"] = "Warding Rune of Glancing",
        ["Барьерная руна сердца"] = "Warding Rune of Heart",
        ["Барьерная руна опустевания"] = "Warding Rune of Hollowing",
        ["Барьерная руна пропитания"] = "Warding Rune of Nourishment",
        ["Барьерная руна одержимости"] = "Warding Rune of Obsession",
        ["Барьерная руна защиты"] = "Warding Rune of Protection",
        ["Барьерная руна укрепления"] = "Warding Rune of Reinforcement",
        ["Барьерная руна обломков"] = "Warding Rune of Salvaging",
        ["Барьерная руна устойчивости"] = "Warding Rune of Stability",
        ["Барьерная руна симбиоза"] = "Warding Rune of Symbiosis",
        ["Руна меткости графини Сеске"] = "Countess Seske's Rune of Archery",
        ["Руна жестокости куртизанки Маннан"] = "Courtesan Mannan's Rune of Cruelty",
        ["Руна восстановления Краценна"] = "Craiceann's Rune of Recovery",
        ["Руна барьера Краценна"] = "Craiceann's Rune of Warding",
        ["Руна грации Фаррул"] = "Farrul's Rune of Grace",
        ["Руна погони Фаррул"] = "Farrul's Rune of the Chase",
        ["Руна охоты Фаррул"] = "Farrul's Rune of the Hunt",
        ["Руна агонии Фенумы"] = "Fenumus' Rune of Agony",
        ["Руна высушивания Фенумы"] = "Fenumus' Rune of Draining",
        ["Руна плетения Фенумы"] = "Fenumus' Rune of Spinning",
        ["Руна мудрости лесной ведьмы Ассандры"] = "Hedgewitch Assandra's Rune of Wisdom",
        ["Руна зимы леди Гестры"] = "Lady Hestra's Rune of Winter",
        ["Руна эрозии Сакаваля"] = "Saqawal's Rune of Erosion",
        ["Руна памяти Сакаваля"] = "Saqawal's Rune of Memory",
        ["Руна неба Сакаваля"] = "Saqawal's Rune of the Sky",
        ["Руна дикости тана Гирта"] = "Thane Girt's Rune of Wildness",
        ["Руна мастерства тана Граннеля"] = "Thane Grannell's Rune of Mastery",
        ["Руна весны тана Лельда"] = "Thane Leld's Rune of Spring",
        ["Руна лета тана Мирка"] = "Thane Myrk's Rune of Summer",
        ["Руна когтей Великого волка"] = "The Greatwolf's Rune of Claws",
        ["Руна воли Великого волка"] = "The Greatwolf's Rune of Willpower",
        ["Изобретательность Астрид"] = "Astrid's Creativity",
        ["Озарение Кадигана"] = "Cadigan's Epiphany",
        ["Мрачность Катлы"] = "Katla's Gloom",
        ["Охота Колра"] = "Kolr's Hunt",
        ["Присмотр Медведя"] = "Medved's Tending",
        ["Триумф Серли"] = "Serle's Triumph",
        ["Сила Трада"] = "Thrud's Might",
        ["Сидерий Утреда"] = "Uhtred's Sidereus",
        ["Резня Вораны"] = "Vorana's Carnage",
        ["Aldur's Legacy"] = "Aldur's Legacy",
        ["Betrayal of Aldur"] = "Betrayal of Aldur",
        ["Breath of Aldur"] = "Breath of Aldur",
        ["Ire of Aldur"] = "Ire of Aldur",
        ["Passion of Aldur"] = "Passion of Aldur",

        // --- Desecration currency (Abyss-family stackable currency, того же раздела) ---
        ["Обглоданная челюсть"] = "Gnawed Jawbone",
        ["Сохранившаяся челюсть"] = "Preserved Jawbone",
        ["Древняя челюсть"] = "Ancient Jawbone",
        ["Обглоданное ребро"] = "Gnawed Rib",
        ["Сохранившееся ребро"] = "Preserved Rib",
        ["Древнее ребро"] = "Ancient Rib",
        ["Обглоданная ключица"] = "Gnawed Collarbone",
        ["Сохранившаяся ключица"] = "Preserved Collarbone",
        ["Древняя ключица"] = "Ancient Collarbone",
        ["Сохранившийся череп"] = "Preserved Cranium",
        ["Сохранившийся позвоночник"] = "Preserved Vertebrae",
        ["Видоизменившаяся ключица"] = "Altered Collarbone",

        // --- Distilled Emotions (24 на вики, 23 здесь — см. примечание про Ancient Liquid Paranoia) ---
        ["Разбавленный жидкий гнев"] = "Diluted Liquid Ire",
        ["Разбавленная жидкая вина"] = "Diluted Liquid Guilt",
        ["Разбавленная жидкая жадность"] = "Diluted Liquid Greed",
        ["Жидкая паранойя"] = "Liquid Paranoia",
        ["Жидкая зависть"] = "Liquid Envy",
        ["Жидкое отвращение"] = "Liquid Disgust",
        ["Жидкое отчаяние"] = "Liquid Despair",
        ["Концентрированный жидкий страх"] = "Concentrated Liquid Fear",
        ["Концентрированное жидкое страдание"] = "Concentrated Liquid Suffering",
        ["Концентрированное жидкое отчуждение"] = "Concentrated Liquid Isolation",
        ["Древний разбавленный жидкий гнев"] = "Ancient Diluted Liquid Ire",
        ["Древняя разбавленная жидкая вина"] = "Ancient Diluted Liquid Guilt",
        ["Древняя разбавленная жидкая жадность"] = "Ancient Diluted Liquid Greed",
        // "Ancient Liquid Paranoia" пропущен — см. комментарий в начале файла.
        ["Древняя жидкая зависть"] = "Ancient Liquid Envy",
        ["Древнее жидкое отвращение"] = "Ancient Liquid Disgust",
        ["Древнее жидкое отчаяние"] = "Ancient Liquid Despair",
        ["Древний концентрированный жидкий страх"] = "Ancient Concentrated Liquid Fear",
        ["Древнее концентрированное жидкое страдание"] = "Ancient Concentrated Liquid Suffering",
        ["Древнее концентрированное жидкое отчуждение"] = "Ancient Concentrated Liquid Isolation",
        ["Густая жидкая меланхолия"] = "Potent Liquid Melancholy",
        ["Густая жидкая свирепость"] = "Potent Liquid Ferocity",
        ["Густое жидкое презрение"] = "Potent Liquid Contempt",
        ["Древняя густая жидкая меланхолия"] = "Ancient Potent Liquid Melancholy",
        ["Древняя густая жидкая свирепость"] = "Ancient Potent Liquid Ferocity",
        ["Древнее густое жидкое презрение"] = "Ancient Potent Liquid Contempt",

        // --- Expedition artifacts (подтверждено) ---
        ["Артефакт Чёрной косы"] = "Black Scythe Artifact",
        ["Артефакт Разомкнутого круга"] = "Broken Circle Artifact",
        ["Артефакт Ордена"] = "Order Artifact",
        ["Артефакт Солнца"] = "Sun Artifact",
        ["Жгучий расплав"] = "Blazing Flux",
        ["Студёный расплав"] = "Chilling Flux",
        ["Искрящий расплав"] = "Crackling Flux",
        ["Пустотный расплав"] = "Void Flux",
        ["Сфера Узазы"] = "Perfect Flux",
        ["Журнал экспедиции"] = "Expedition Logbook",
        ["Сага Медведя"] = "Medved's Saga",
        ["Сага Олрота"] = "Olroth's Saga",
        ["Сага Утреда"] = "Uhtred's Saga",
        ["Сага Вораны"] = "Vorana's Saga",
        ["Aldur's Saga"] = "Aldur's Saga",

        // --- Verisium / рунекузнечество (подтверждено) ---
        ["Веризий"] = "Verisium",
        ["Исключительный веризий"] = "Exceptional Verisium",
        ["Рунный сплав"] = "Runic Alloy",
        ["Знак Круга Медведя"] = "Medved's Crest of the Circle",
        ["Знак Косы Вораны"] = "Vorana's Crest of the Scythe",
        ["Знак Чаши Утреда"] = "Uhtred's Crest of the Chalice",
        ["Знак Солнца Олрота"] = "Olroth's Crest of the Sun",
        ["Адаптивный сплав"] = "Adaptive Alloy",
        ["Небесный сплав"] = "Celestial Alloy",
        ["Вихревой сплав"] = "Cyclonic Alloy",
        ["Экспансивный сплав"] = "Expansive Alloy",
        ["Мистический сплав"] = "Mystic Alloy",
        ["Радужный сплав"] = "Prismatic Alloy",
        ["Защитный сплав"] = "Protective Alloy",
        ["Державный сплав"] = "Sovereign Alloy",
        ["Лёгкий сплав"] = "Swift Alloy",
        ["Возвышенный сплав"] = "Transcendent Alloy",
        ["Сплав Повелителя рун"] = "The Runebinder's Alloy",
        ["Сплав Рунного отца"] = "The Runefather's Alloy",
        ["Благоговейная подзвёздная руда"] = "Revered Starlit Ore",
        ["Почитаемая подзвёздная руда"] = "Venerable Starlit Ore",
        ["Истинная подзвёздная руда"] = "Veridical Starlit Ore",
        ["Оберегающая подзвёздная руда"] = "Warding Starlit Ore",

        // --- Чародейский расплав (Kalguuran-гем-валюта, 20 уровней, заголовки подтверждены) ---
        ["Чародейский расплав (Уровень 1)"] = "Thaumaturgic Flux (Level 1)",
        ["Чародейский расплав (Уровень 2)"] = "Thaumaturgic Flux (Level 2)",
        ["Чародейский расплав (Уровень 3)"] = "Thaumaturgic Flux (Level 3)",
        ["Чародейский расплав (Уровень 4)"] = "Thaumaturgic Flux (Level 4)",
        ["Чародейский расплав (Уровень 5)"] = "Thaumaturgic Flux (Level 5)",
        ["Чародейский расплав (Уровень 6)"] = "Thaumaturgic Flux (Level 6)",
        ["Чародейский расплав (Уровень 7)"] = "Thaumaturgic Flux (Level 7)",
        ["Чародейский расплав (Уровень 8)"] = "Thaumaturgic Flux (Level 8)",
        ["Чародейский расплав (Уровень 9)"] = "Thaumaturgic Flux (Level 9)",
        ["Чародейский расплав (Уровень 10)"] = "Thaumaturgic Flux (Level 10)",
        ["Чародейский расплав (Уровень 11)"] = "Thaumaturgic Flux (Level 11)",
        ["Чародейский расплав (Уровень 12)"] = "Thaumaturgic Flux (Level 12)",
        ["Чародейский расплав (Уровень 13)"] = "Thaumaturgic Flux (Level 13)",
        ["Чародейский расплав (Уровень 14)"] = "Thaumaturgic Flux (Level 14)",
        ["Чародейский расплав (Уровень 15)"] = "Thaumaturgic Flux (Level 15)",
        ["Чародейский расплав (Уровень 16)"] = "Thaumaturgic Flux (Level 16)",
        ["Чародейский расплав (Уровень 17)"] = "Thaumaturgic Flux (Level 17)",
        ["Чародейский расплав (Уровень 18)"] = "Thaumaturgic Flux (Level 18)",
        ["Чародейский расплав (Уровень 19)"] = "Thaumaturgic Flux (Level 19)",
        ["Чародейский расплав (Уровень 20)"] = "Thaumaturgic Flux (Level 20)",

        // --- UncutGems — НЕ подтверждено официальным источником, см. примечание в начале файла ---
        ["Неограненный камень умения"] = "Uncut Skill Gem",
        ["Неограненный камень духа"] = "Uncut Spirit Gem",
        ["Неограненный камень поддержки"] = "Uncut Support Gem",
        ["Неограненный камень умения (Уровень 1)"] = "Uncut Skill Gem (Level 1)",
        ["Неограненный камень умения (Уровень 2)"] = "Uncut Skill Gem (Level 2)",
        ["Неограненный камень умения (Уровень 3)"] = "Uncut Skill Gem (Level 3)",
        ["Неограненный камень умения (Уровень 4)"] = "Uncut Skill Gem (Level 4)",
        ["Неограненный камень умения (Уровень 5)"] = "Uncut Skill Gem (Level 5)",
        ["Неограненный камень умения (Уровень 6)"] = "Uncut Skill Gem (Level 6)",
        ["Неограненный камень умения (Уровень 7)"] = "Uncut Skill Gem (Level 7)",
        ["Неограненный камень умения (Уровень 8)"] = "Uncut Skill Gem (Level 8)",
        ["Неограненный камень умения (Уровень 9)"] = "Uncut Skill Gem (Level 9)",
        ["Неограненный камень умения (Уровень 10)"] = "Uncut Skill Gem (Level 10)",
        ["Неограненный камень умения (Уровень 11)"] = "Uncut Skill Gem (Level 11)",
        ["Неограненный камень умения (Уровень 12)"] = "Uncut Skill Gem (Level 12)",
        ["Неограненный камень умения (Уровень 13)"] = "Uncut Skill Gem (Level 13)",
        ["Неограненный камень умения (Уровень 14)"] = "Uncut Skill Gem (Level 14)",
        ["Неограненный камень умения (Уровень 15)"] = "Uncut Skill Gem (Level 15)",
        ["Неограненный камень умения (Уровень 16)"] = "Uncut Skill Gem (Level 16)",
        ["Неограненный камень умения (Уровень 17)"] = "Uncut Skill Gem (Level 17)",
        ["Неограненный камень умения (Уровень 18)"] = "Uncut Skill Gem (Level 18)",
        ["Неограненный камень умения (Уровень 19)"] = "Uncut Skill Gem (Level 19)",
        ["Неограненный камень умения (Уровень 20)"] = "Uncut Skill Gem (Level 20)",
        ["Неограненный камень духа (Уровень 4)"] = "Uncut Spirit Gem (Level 4)",
        ["Неограненный камень духа (Уровень 5)"] = "Uncut Spirit Gem (Level 5)",
        ["Неограненный камень духа (Уровень 6)"] = "Uncut Spirit Gem (Level 6)",
        ["Неограненный камень духа (Уровень 7)"] = "Uncut Spirit Gem (Level 7)",
        ["Неограненный камень духа (Уровень 8)"] = "Uncut Spirit Gem (Level 8)",
        ["Неограненный камень духа (Уровень 9)"] = "Uncut Spirit Gem (Level 9)",
        ["Неограненный камень духа (Уровень 10)"] = "Uncut Spirit Gem (Level 10)",
        ["Неограненный камень духа (Уровень 11)"] = "Uncut Spirit Gem (Level 11)",
        ["Неограненный камень духа (Уровень 12)"] = "Uncut Spirit Gem (Level 12)",
        ["Неограненный камень духа (Уровень 13)"] = "Uncut Spirit Gem (Level 13)",
        ["Неограненный камень духа (Уровень 14)"] = "Uncut Spirit Gem (Level 14)",
        ["Неограненный камень духа (Уровень 15)"] = "Uncut Spirit Gem (Level 15)",
        ["Неограненный камень духа (Уровень 16)"] = "Uncut Spirit Gem (Level 16)",
        ["Неограненный камень духа (Уровень 17)"] = "Uncut Spirit Gem (Level 17)",
        ["Неограненный камень духа (Уровень 18)"] = "Uncut Spirit Gem (Level 18)",
        ["Неограненный камень духа (Уровень 19)"] = "Uncut Spirit Gem (Level 19)",
        ["Неограненный камень духа (Уровень 20)"] = "Uncut Spirit Gem (Level 20)",
        ["Неограненный камень поддержки (Уровень 1)"] = "Uncut Support Gem (Level 1)",
        ["Неограненный камень поддержки (Уровень 2)"] = "Uncut Support Gem (Level 2)",
        ["Неограненный камень поддержки (Уровень 3)"] = "Uncut Support Gem (Level 3)",
        ["Неограненный камень поддержки (Уровень 4)"] = "Uncut Support Gem (Level 4)",
        ["Неограненный камень поддержки (Уровень 5)"] = "Uncut Support Gem (Level 5)",
    };

    // Минимальная схожесть (1 - расстояние Левенштейна / длина) для fuzzy-совпадения по RU-словарю.
    // То же значение, что ScanEngine.FuzzyThreshold — нет причин делать русский путь менее
    // терпимым к ошибкам распознавания, чем английский.
    private const double FuzzyThreshold = 0.84;

    // RU (нормализовано) -> EN (нормализовано), построено один раз через ту же NormalizeName,
    // что причёсывает реальный OCR-текст — так ключи здесь гарантированно совпадают по формату
    // с тем, что приходит из сканера.
    private static readonly Dictionary<string, string> _normalized = BuildNormalized();

    private static Dictionary<string, string> BuildNormalized()
    {
        var dict = new Dictionary<string, string>();
        foreach (var (ru, en) in Map)
        {
            var ruKey = OcrScanner.NormalizeName(ru);
            var enKey = OcrScanner.NormalizeName(en);
            if (!string.IsNullOrEmpty(ruKey) && !string.IsNullOrEmpty(enKey))
                dict[ruKey] = enKey;
        }
        return dict;
    }

    // Переводит нормализованную кириллическую строку OCR в нормализованное каноническое EN-имя,
    // или возвращает null, если ничего не подошло. Сначала точное совпадение, затем fuzzy по
    // самим RU-ключам (расстояние Левенштейна) — это спасает от мелких ошибок распознавания
    // кириллицы так же, как ScanEngine.BestFuzzy спасает английский путь, только сравнение идёт
    // с русскими именами из словаря, а не с английскими ключами цен.
    public static string? Resolve(string normalizedRuName)
    {
        if (string.IsNullOrEmpty(normalizedRuName)) return null;
        if (_normalized.TryGetValue(normalizedRuName, out var exact)) return exact;

        string? best = null;
        double bestScore = FuzzyThreshold;
        foreach (var (ruKey, enKey) in _normalized)
        {
            if (Math.Abs(ruKey.Length - normalizedRuName.Length) > 3) continue;
            int dist = ScanEngine.Levenshtein(normalizedRuName, ruKey);
            double score = 1.0 - (double)dist / Math.Max(normalizedRuName.Length, ruKey.Length);
            if (score > bestScore) { bestScore = score; best = enKey; }
        }
        return best;
    }
}
