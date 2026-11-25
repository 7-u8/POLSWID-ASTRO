namespace ASTRO.Polswid.App.Localization;

/// <summary>One setting explanation: what / what changes / do I need it / when to use / risks.</summary>
public sealed record HelpTopic(string Id, string Title, string What, string Effect, string Need, string Example, string Risk)
{
    public string SearchText => $"{Title} {What} {Effect} {Need} {Example} {Risk}";
}

public sealed record FaqItem(string Question, string Answer)
{
    public string SearchText => Question + " " + Answer;
}

public static class HelpContent
{
    // order of fields: title, what, effect, need, example, risk
    static readonly Dictionary<string, (string[] ar, string[] en)> Topics = new();
    static void T(string id, string[] ar, string[] en) => Topics[id] = (ar, en);

    public static HelpTopic? Get(string id)
    {
        if (!Topics.TryGetValue(id, out var t)) return null;
        var a = Loc.Instance.IsArabic ? t.ar : t.en;
        return new HelpTopic(id, a[0], a[1], a[2], a[3], a[4], a[5]);
    }

    public static IEnumerable<HelpTopic> AllTopics() => Topics.Keys.Select(k => Get(k)!);
    public static IEnumerable<string> TopicIds => Topics.Keys;
    public static IEnumerable<(string id, string[] ar, string[] en)> RawTopics => Topics.Select(kv => (kv.Key, kv.Value.ar, kv.Value.en));
    public static (string q, string a)[] RawFaq(bool arabic) => arabic ? FaqAr : FaqEn;

    public static IEnumerable<FaqItem> Faq()
    {
        var l = Loc.Instance.IsArabic ? FaqAr : FaqEn;
        return l.Select(x => new FaqItem(x.q, x.a));
    }

    static HelpContent()
    {
        T("enable",
            new[] { "تفعيل ASTRO POLSWID",
                "هذا هو المفتاح الرئيسي. عند تشغيله يبدأ البرنامج بتحويل حركات يدك الحقيقية إلى الأزرار التي حدّدتها للعبة.",
                "تصل اللعبة بالأزرار المحوَّلة (مثلاً R2 يصبح السهم للأسفل). عند الإيقاف تصل حركاتك للعبة كما هي دون أي تغيير.",
                "نعم، هو الزر الأساسي. اتركه مفعّلاً أثناء اللعب.",
                "فعّله قبل بدء السباق، وأوقفه إذا أردت اللعب بشكل عادي أو استخدام اليد في لعبة أخرى.",
                "لا. البرنامج لا يضغط أي زر بدلاً منك، فقط يحوّل ما تضغطه أنت. وإن حدث خلل اضغط «إيقاف طارئ»." },
            new[] { "Turn ASTRO POLSWID on",
                "The main switch. When on, the app translates your real controller's buttons into the ones you configured for the game.",
                "The game receives the translated buttons (for example R2 becomes D-Pad Down). When off, your input reaches the game unchanged.",
                "Yes, it is the main control. Keep it on while playing.",
                "Turn it on before a race; turn it off to play normally or to use the controller in another game.",
                "No. The app never presses a button for you, it only translates what you press. If anything feels wrong press “Emergency stop”." });

        T("physical",
            new[] { "اختيار يد التحكم الحقيقية",
                "اليد الحقيقية هي التي تمسكها بيدك (PlayStation أو Xbox أو غيرها). من هنا تختار أي يد يقرأ منها البرنامج.",
                "إذا اخترت يداً محددة فسيقرأ البرنامج منها فقط ويتجاهل أي يد أخرى. وإذا اخترت «تلقائي» فسيستخدم أول يد يجدها ويبقى عليها.",
                "غالباً لا. «تلقائي» مناسب إذا كانت عندك يد واحدة.",
                "إذا وصّلت يدين وتريد استخدام إحداهما فقط، اخترها من القائمة.",
                "إذا فصلت اليد المختارة سيتوقف الإدخال ولن ينتقل البرنامج إلى يد أخرى تلقائياً. هذا مقصود لحمايتك من خلط الإدخال." },
            new[] { "Choose your real controller",
                "Your real controller is the one in your hands (PlayStation, Xbox or another). Here you choose which one the app reads.",
                "With a specific controller selected, only that one is read and others are ignored. With “Automatic” the first one found is used and kept.",
                "Usually not. “Automatic” is fine with one controller.",
                "If two controllers are connected and you want only one of them, pick it here.",
                "If the selected controller is unplugged, input stops and the app will not jump to another one. This is on purpose, so inputs are never mixed." });

        T("virtual",
            new[] { "اختيار وحدة التحكم الافتراضية",
                "هذه يد وهمية (غير موجودة فعلياً) ينشئها البرنامج وتراها اللعبة على أنها يد حقيقية. تستقبل الأزرار بعد تحويلها.",
                "تتعرّف اللعبة على النوع الذي تختاره: Xbox 360 أو DualShock 4. لا علاقة لهذا الاختيار بنوع يدك الحقيقية.",
                "نعم، اختر نوعاً واحداً. الإعداد الافتراضي DualShock 4 (يعرض أزرار PlayStation). اختر Xbox 360 إذا كانت اللعبة لا تتعرف عليه أو تفضّل أزرار Xbox.",
                "تلعب بيد Xbox لكن تريد أن تظهر أزرار PlayStation في اللعبة: اختر DualShock 4.",
                "تغيير النوع يعيد إنشاء اليد الافتراضية، وقد تحتاج اللعبة إلى إعادة التعرّف عليها (أحياناً إعادة تشغيل اللعبة)." },
            new[] { "Choose the virtual controller",
                "A pretend controller the app creates and the game sees as a real one. It receives your buttons after translation.",
                "The game recognises the type you pick: Xbox 360 or DualShock 4. This has nothing to do with your real controller's brand.",
                "Yes, pick one. The default is DualShock 4 (shows PlayStation buttons). Pick Xbox 360 if the game does not recognise it or you prefer Xbox buttons.",
                "You play with an Xbox pad but want PlayStation buttons in the game: choose DualShock 4.",
                "Changing the type recreates the virtual controller, and the game may need to detect it again (sometimes restart the game)." });

        T("analog",
            new[] { "تحويل الأنالوق الأيسر إلى أزرار الاتجاهات",
                "عند تحريك الأنالوق الأيسر يميناً أو يساراً، يحوّل البرنامج الحركة إلى ضغطة على زر الاتجاه (السهم الأيمن أو الأيسر) داخل وحدة التحكم الافتراضية.",
                "يعمل الأنالوق الأيسر كأنه أسهم اتجاهات. وتُخفى حركته الأفقية الأصلية عن اللعبة، بينما تبقى حركته للأعلى والأسفل كما هي.",
                "اختياري. فعّله فقط إذا أردت التوجيه بالأنالوق الأيسر بدل الأسهم.",
                "في لعبة سباق تحتاج فيها أسهم الاتجاهات، لكنك تفضل تحريك الأنالوق بإبهامك.",
                "الحركة الأفقية للأنالوق الأيسر لن تصل للعبة أثناء التفعيل. إن كانت اللعبة تستخدمها للتحرك يميناً ويساراً فلن تتحرك الشخصية أفقياً بالأنالوق أثناء التفعيل. أما حركته للأعلى والأسفل فتبقى كما هي." },
            new[] { "Left analog → direction buttons",
                "When you push the left stick left or right, the app turns it into a press of the D-Pad Left or Right button on the virtual controller.",
                "The left stick works like arrow buttons. Its original sideways movement is hidden from the game, while up/down movement is passed on unchanged.",
                "Optional. Turn it on only if you want to steer with the left stick instead of the arrows.",
                "In a racing game that needs the direction arrows, but you prefer moving a stick with your thumb.",
                "The left stick's sideways movement will not reach the game while this is on. If the game uses it for sideways movement, that stops while this is on. Up/down movement still passes through." });

        T("r2",
            new[] { "سلوك زر R2",
                "في الإعداد الافتراضي يتحول ضغط R2 إلى «السهم للأسفل». هنا تختار هل يبقى ما دمت ضاغطاً (ضغط مستمر) أم يعمل كمفتاح يشغّل ويطفئ (تبديل).",
                "ضغط مستمر: السهم للأسفل مضغوط فقط طالما R2 مضغوط. تبديل: ضغطة واحدة تثبّته، وضغطة أخرى تحرره.",
                "لا. اترك «ضغط مستمر» (الموصى به).",
                "اختر «تبديل» إذا كان الضغط المستمر على R2 يتعبك.",
                "في وضع التبديل قد يبقى السهم مضغوطاً دون أن تنتبه. إن حدث ذلك اضغط R2 مرة أخرى أو «إيقاف طارئ»." },
            new[] { "R2 behaviour",
                "By default pressing R2 becomes “D-Pad Down”. Here you choose whether it lasts while you hold it (hold) or works like a switch (toggle).",
                "Hold: D-Pad Down is pressed only while R2 is held. Toggle: one press latches it, another releases it.",
                "No. Keep “Hold” (recommended).",
                "Choose “Toggle” if holding R2 tires your finger.",
                "In toggle mode the arrow may stay pressed without you noticing. If so, press R2 again or use “Emergency stop”." });

        T("continuous",
            new[] { "التوجيه المستمر مع R2",
                "ميزة اختيارية تجعل اتجاه التوجيه (يمين أو يسار) لا يصبح «محايداً» أبداً أثناء ضغطك على R2، وتعمل بالأنالوق الأيسر فقط.",
                "ما دمت ضاغطاً على R2: يُضغط السهم للأسفل فوراً، ثم المربع بعد مهلة قصيرة تحددها. وإذا كان الأنالوق الأيسر ثابتاً يُرسل السهم الأيسر، وإذا حرّكته يميناً يُرسل السهم الأيمن، وإذا حرّكته يساراً يُرسل السهم الأيسر. عند ترك R2 يُرفع كل شيء ويعود الأنالوق الأيسر لعمله الطبيعي.",
                "لا. معطّلة افتراضياً. فعّلها فقط إذا كنت تحتاج هذا الأسلوب في اللعب.",
                "تريد أن يبقى التوجيه مضغوطاً دائماً أثناء R2 دون أن تفلت الاتجاه، وتغييره بحركة الأنالوق الأيسر.",
                "أثناء ضغط R2 لا تصل حركة الأنالوق الأيسر الأفقية للعبة، ويُضغط المربع تلقائياً. الأنالوق الأيمن لا يتأثر أبداً. وتبقى النتيجة داخل اللعبة بحاجة لتجربتك." },
            new[] { "Continuous steering with R2",
                "An optional feature that keeps steering (left or right) from ever being neutral while you hold R2. It uses the left stick only.",
                "While R2 is held: D-Pad Down is pressed at once, then Square after a short delay you set. A still left stick sends D-Pad Left; moved right sends Right; moved left sends Left. When you release R2 everything is released and the left stick works normally again.",
                "No. It is off by default. Turn it on only if you want this style of play.",
                "You want steering held at all times while R2 is down and changed by moving the left stick.",
                "While R2 is held the left stick's sideways movement does not reach the game, and Square is pressed automatically. The right stick is never touched. The in-game result still needs your own testing." });

        T("passthrough",
            new[] { "إرسال قيمة R2 الأصلية أيضاً",
                "R2 زناد يعطي قيمة متدرجة (من خفيف إلى كامل). افتراضياً لا يرسل البرنامج هذه القيمة للعبة بل يحوّلها إلى السهم فقط.",
                "عند التفعيل تصل اللعبة قيمة R2 الأصلية مع السهم للأسفل في الوقت نفسه.",
                "لا. اتركه متوقفاً.",
                "فقط إذا كانت اللعبة تحتاج R2 نفسه بالإضافة إلى السهم للأسفل.",
                "قد تتصرف اللعبة بطريقة غير متوقعة لأن حركة واحدة تصل كزرّين معاً." },
            new[] { "Also send the original R2 value",
                "R2 is a trigger with a gradual value (light to full). By default the app does not send that value, it only turns it into the arrow.",
                "When on, the game gets the original R2 value together with D-Pad Down.",
                "No. Leave it off.",
                "Only if the game needs R2 itself in addition to D-Pad Down.",
                "The game may behave unexpectedly because one movement arrives as two buttons." });

        T("trigth",
            new[] { "حدّا ضغط الزناد وإفلاته",
                "R2 زناد متدرج. هذان الحدّان يحددان متى يعتبره البرنامج «مضغوطاً» ومتى «متروكاً».",
                "حد الضغط أعلى من حد الإفلات حتى لا يتذبذب الزر عند الضغط الخفيف.",
                "لا. القيم الموصى بها: 0.30 للضغط و0.20 للإفلات.",
                "إذا كان زنادك حساساً جداً أو ثقيلاً، عدّل الحد قليلاً.",
                "قيم قريبة جداً من بعضها قد تسبب تذبذب السهم للأسفل." },
            new[] { "Trigger press and release levels",
                "R2 is a gradual trigger. These two levels decide when the app treats it as “pressed” and “released”.",
                "The press level is higher than the release level so the button does not flicker on a light press.",
                "No. Recommended: 0.30 to press and 0.20 to release.",
                "If your trigger is very sensitive or very stiff, adjust a little.",
                "Values too close together can make D-Pad Down flicker." });

        T("deadzone",
            new[] { "المنطقة الميتة",
                "مساحة صغيرة حول منتصف الأنالوق يتجاهل فيها البرنامج الحركة، لأن الأنالوق قد يهتز قليلاً حتى لو لم تلمسه.",
                "كلما زادت القيمة احتجت إلى تحريك أكبر ليبدأ البرنامج بالاستجابة.",
                "لا. القيمة الموصى بها 0.20.",
                "إذا كان الأنالوق يتحرك وحده، ارفعها قليلاً. وإذا كان يحتاج تحريكاً كبيراً، اخفضها.",
                "قيمة صغيرة جداً قد تسبب اتجاهاً خاطئاً بسبب اهتزاز الأنالوق، وقيمة كبيرة جداً تجعل التوجيه ثقيلاً." },
            new[] { "Dead zone",
                "A small area around the stick's centre where movement is ignored, because sticks can wobble even when untouched.",
                "The larger the value, the more you must move the stick before the app reacts.",
                "No. Recommended: 0.20.",
                "If the stick drifts by itself, raise it a little. If it needs a big push, lower it.",
                "Too small can cause wrong directions from stick wobble; too large makes steering feel heavy." });

        T("activation",
            new[] { "حد التفعيل",
                "المسافة التي يجب أن تحرك فيها الأنالوق ليعتبره البرنامج «ضغطة اتجاه».",
                "قيمة أعلى = يجب دفع الأنالوق أكثر. قيمة أقل = يستجيب بحركة خفيفة.",
                "لا. الموصى به 0.60.",
                "إذا أردت أن يستجيب بحركة صغيرة، اخفضه.",
                "إن جعلته منخفضاً جداً قد يتغير الاتجاه دون قصد." },
            new[] { "Activation level",
                "How far you must push the stick before the app counts it as a direction press.",
                "Higher = push further. Lower = reacts to a light movement.",
                "No. Recommended: 0.60.",
                "Lower it if you want a small movement to count.",
                "Too low and the direction may change by accident." });

        T("release",
            new[] { "حد الإفلات",
                "المسافة التي يعود عندها الأنالوق نحو المنتصف ليعتبره البرنامج أنك أفلتّ الاتجاه. يجب أن يكون أقل من حد التفعيل.",
                "يمنع الاتجاه من «الارتعاش» بين الضغط والإفلات عندما يكون الأنالوق قرب الحد.",
                "لا. الموصى به 0.35.",
                "إذا كان الاتجاه يتقطع، قلّل الحد قليلاً.",
                "إذا اقترب من حد التفعيل تعود مشكلة الارتعاش." },
            new[] { "Release level",
                "How close to the centre the stick must come back for the app to treat the direction as released. It must stay below the activation level.",
                "Prevents the direction from flickering between pressed and released near the limit.",
                "No. Recommended: 0.35.",
                "If the direction keeps cutting out, lower it slightly.",
                "If it gets close to the activation level the flicker returns." });

        T("sensitivity",
            new[] { "الحساسية",
                "تُضخّم أو تُضعف حركة الأنالوق قبل أن يحكم عليها البرنامج.",
                "حساسية أعلى = الوصول للحد أسرع بحركة أقل.",
                "لا. الموصى به 1.00.",
                "إذا كان إبهامك لا يصل إلى أقصى الأنالوق بسهولة، ارفعها قليلاً.",
                "حساسية عالية جداً تجعل أي لمسة تُحسب اتجاهاً." },
            new[] { "Sensitivity",
                "Boosts or reduces the stick's movement before the app judges it.",
                "Higher = reaches the limit faster with less movement.",
                "No. Recommended: 1.00.",
                "If your thumb can't easily reach the stick's edge, raise it a little.",
                "Very high sensitivity makes any touch count as a direction." });

        T("curve",
            new[] { "منحنى الاستجابة",
                "يحدد كيف تتحول حركتك إلى قيمة: 1 = بشكل مباشر، وأكبر من 1 = تحتاج دفعاً أكبر قبل أن يستجيب.",
                "يغيّر إحساس الأنالوق قرب المنتصف دون تغيير النهاية.",
                "لا. الموصى به 1.00.",
                "نادراً. جرّبه إذا كانت الحركات الخفيفة تُحسب بسرعة كبيرة.",
                "قد يجعل الاستجابة غير مألوفة." },
            new[] { "Response curve",
                "Decides how your movement becomes a value: 1 = direct, above 1 = needs a bigger push before it reacts.",
                "Changes how the stick feels near the centre without changing the end.",
                "No. Recommended: 1.00.",
                "Rarely. Try it if light movements count too quickly.",
                "May make the response feel unfamiliar." });

        T("smoothing",
            new[] { "التنعيم",
                "يُنعّم حركة الأنالوق ليقلل الارتعاش.",
                "الحركة تصبح أهدأ لكنها تتأخر قليلاً.",
                "لا. الموصى به 0 (متوقف).",
                "إذا كان الأنالوق مهتزاً جداً وصعب التحكم.",
                "يضيف تأخراً صغيراً في الاستجابة، وهذا غير مناسب للسباقات السريعة." },
            new[] { "Smoothing",
                "Smooths stick movement to reduce jitter.",
                "Movement becomes calmer but slightly delayed.",
                "No. Recommended: 0 (off).",
                "If the stick is very shaky and hard to control.",
                "Adds a small delay, which is not ideal for fast racing." });

        T("invert",
            new[] { "عكس الاتجاه",
                "يبدّل اليمين باليسار في تحويل الأنالوق.",
                "تحريك الأنالوق يميناً يعطي السهم الأيسر والعكس.",
                "لا.",
                "لا تحتاجه عادة.",
                "قد يربكك إذا نسيت أنه مفعّل." },
            new[] { "Swap left and right",
                "Swaps left and right in the stick conversion.",
                "Pushing the stick right gives D-Pad Left and the other way round.",
                "No.",
                "Normally never.",
                "Can confuse you if you forget it is on." });

        T("suppress",
            new[] { "إخفاء حركة الأنالوق الأصلية",
                "عند تحويل الأنالوق إلى أسهم، يقرر البرنامج هل يرسل للعبة أيضاً الحركة الأفقية الأصلية للأنالوق.",
                "مفعّل: ترى اللعبة الأسهم فقط (الموصى به). متوقف: ترى الأسهم والحركة الأصلية معاً.",
                "اتركه مفعّلاً.",
                "أوقفه فقط للتجربة.",
                "إيقافه قد يجعل اللعبة تتلقى توجيهين معاً." },
            new[] { "Hide the original analog movement",
                "When the stick is converted to arrows, this decides whether the game also receives the stick's original sideways movement.",
                "On: the game sees only the arrows (recommended). Off: it sees the arrows and the original movement together.",
                "Leave it on.",
                "Turn it off only for experiments.",
                "Turning it off may make the game receive two steering inputs." });

        T("priority",
            new[] { "من يقود الاتجاه؟",
                "يمكنك تحديد الاتجاه بالأسهم أو بالأنالوق الأيسر. عندما يطلب الاثنان اتجاهين مختلفين، هذا الخيار يحدد من يفوز.",
                "«الأنالوق أولاً»: الأنالوق يقود وعند تركه تعود القيادة للأسهم. «الأسهم أولاً»: العكس. «آخر ما استخدمته»: يفوز آخر مصدر لمسته.",
                "لا. الموصى به «الأنالوق أولاً».",
                "إذا كنت تستخدم الأسهم غالباً وتريدها أن تتغلب دائماً، اختر «الأسهم أولاً».",
                "لا. البرنامج لا يرسل يميناً ويساراً معاً أبداً (إلا في الخيار التجريبي)." },
            new[] { "Who controls steering?",
                "You can steer with the arrows or the left stick. When they ask for different directions, this decides who wins.",
                "“Analog first”: the stick leads and when released the arrows take over. “D-Pad first”: the opposite. “Last touched”: the most recently used source wins.",
                "No. Recommended: “Analog first”.",
                "If you mostly use the arrows and want them to always win, choose “D-Pad first”.",
                "No. The app never sends left and right together (except in the experimental option)." });

        T("contra",
            new[] { "السماح بيمين ويسار معاً (تجريبي)",
                "خيار تجريبي يسمح بإرسال الاتجاهين معاً للعبة.",
                "ترى اللعبة السهم الأيمن والأيسر مضغوطين في الوقت نفسه.",
                "لا. اتركه متوقفاً.",
                "للتجارب فقط.",
                "قد تتصرف اللعبة بشكل غير متوقع أو تتجاهل الإدخال." },
            new[] { "Allow left and right together (experimental)",
                "An experimental option that lets both directions be sent to the game at once.",
                "The game sees D-Pad Left and Right pressed at the same time.",
                "No. Keep it off.",
                "For experiments only.",
                "The game may behave unpredictably or ignore the input." });

        T("dwell",
            new[] { "أقل مدة قبل تغيير الاتجاه",
                "مدة تمنع تغيير الاتجاه بسرعة كبيرة جداً بعد آخر تغيير (بالميلي ثانية).",
                "0 = يتغير الاتجاه فوراً. قيمة أكبر = يتأخر التغيير حتى تمر المدة.",
                "لا. الموصى به 0.",
                "فقط إذا أردت تقليل التغييرات السريعة الناتجة عن ارتعاش.",
                "أي قيمة أكبر من 0 تؤخر استجابة التوجيه." },
            new[] { "Minimum time before direction change",
                "A time that blocks very fast direction changes right after the last one (in milliseconds).",
                "0 = direction changes immediately. Higher = the change waits until the time has passed.",
                "No. Recommended: 0.",
                "Only if you want to reduce fast flips caused by jitter.",
                "Any value above 0 delays steering response." });

        T("uturn",
            new[] { "مساعد الدوران الأرضي (U-Turn)",
                "ميزة اختيارية: عند ضغطك على زر التشغيل الذي تحدده، يرفع البرنامج زراً معيناً (غالباً X) لمدة قصيرة ثم يعيده.",
                "لا يعمل إلا عندما تضغط زر التشغيل أنت. يتحكم في زر واحد فقط ولمدة محددة بالميلي ثانية.",
                "لا. معطّل افتراضياً.",
                "للمستخدمين المتقدمين الذين جرّبوا مدة الإفلات ويعرفون القيمة المناسبة لهم.",
                "المدة المناسبة تختلف بين الألعاب والأجهزة ولا يعرفها البرنامج. جرّبها بنفسك." },
            new[] { "Grounded U-Turn helper",
                "An optional feature: when you press the trigger button you chose, the app releases one button (usually X) for a short time and then restores it.",
                "It only runs when you press the trigger. It controls just one button, for a time you set in milliseconds.",
                "No. It is off by default.",
                "For advanced users who tested the release time and know the value that suits them.",
                "The right time differs between games and PCs and the app cannot know it. Test it yourself." });

        T("air",
            new[] { "مساعد التبديل في الهواء",
                "ميزة اختيارية: عند ضغطك على زر التشغيل ينفّذ البرنامج قفزة ثم يبدّل الاتجاه بعد مدة تحددها.",
                "يضغط زر القفز لمدة قصيرة ثم يقلب الاتجاه (يمين ↔ يسار) في تحديث واحد.",
                "لا. معطّل افتراضياً.",
                "للمستخدمين المتقدمين الذين يعرفون التوقيت المناسب.",
                "البرنامج لا يعرف متى تهبط السيارة، فالتوقيت عليك أنت وعلى التجربة." },
            new[] { "Air-switch helper",
                "An optional feature: when you press the trigger button, the app presses jump and then flips the direction after a time you set.",
                "It presses the jump button briefly, then flips the direction (right ↔ left) in a single update.",
                "No. It is off by default.",
                "For advanced users who know the right timing.",
                "The app cannot tell when the car lands, so timing is up to you and testing." });

        T("emergency",
            new[] { "الإيقاف الطارئ وأزرار الأمان",
                "زر أو اختصار يوقف التحويل فوراً ويرسل للعبة حركاتك الأصلية كما هي.",
                "يتوقف التحويل، ولا يبقى أي زر مضغوطاً. ولا يعود التحويل حتى تفعّله أنت من جديد.",
                "مفيد كإجراء أمان. الاختصار Ctrl+Alt+F10 يعمل دائماً.",
                "إذا شعرت أن شيئاً غير طبيعي أثناء اللعب.",
                "لا. لكن تأكد أن الزر الذي تختاره لا تحتاجه في اللعبة، لأنه يُخفى عنها." },
            new[] { "Emergency stop and safety buttons",
                "A button or shortcut that stops translation immediately and sends your raw input to the game.",
                "Translation stops and nothing stays pressed. It does not come back until you turn it on again.",
                "Useful as a safety measure. The shortcut Ctrl+Alt+F10 always works.",
                "If something feels wrong while playing.",
                "No. But make sure the button you pick isn't needed in the game, since it is hidden from it." });

        T("consume",
            new[] { "إخفاء أزرار المساعدات عن اللعبة",
                "الأزرار التي تخصصها لتشغيل المساعدات أو الإيقاف الطارئ لن تصل للعبة.",
                "عند الضغط عليها يتصرف البرنامج فقط ولا تتفاعل معها اللعبة.",
                "اتركه مفعّلاً.",
                "لو خصصت L1 للمساعد، لن تحسبها اللعبة ضغطة L1.",
                "الزر المخصص لن يعمل داخل اللعبة طالما المساعد مفعّل." },
            new[] { "Hide helper buttons from the game",
                "Buttons you assign to run helpers or the emergency stop will not reach the game.",
                "Pressing them only affects the app; the game does not react.",
                "Leave it on.",
                "If you assign L1 to a helper, the game will not count it as an L1 press.",
                "The assigned button will not work inside the game while the helper is enabled." });

        T("polling",
            new[] { "عدد قراءات اليد في الثانية",
                "عدد المرات التي يفحص فيها البرنامج يدك كل ثانية.",
                "رقم أكبر = استجابة أسرع قليلاً واستهلاك أكبر للمعالج.",
                "لا. الموصى به 500.",
                "نادراً. ارفعه فقط إذا كنت تعرف أن يدك ترسل بسرعة أكبر.",
                "القيم العالية جداً تستهلك المعالج دون فائدة ملموسة." },
            new[] { "Controller reads per second",
                "How many times per second the app checks your controller.",
                "Higher = slightly faster response and more CPU use.",
                "No. Recommended: 500.",
                "Rarely. Raise it only if you know your controller reports faster.",
                "Very high values use CPU without a noticeable benefit." });

        T("submit",
            new[] { "أقصى عدد تحديثات للعبة في الثانية",
                "أقصى عدد من المرات في الثانية يرسل فيها البرنامج الحالة الجديدة للعبة. تُرسل فقط عندما يتغير شيء.",
                "يحدّ من سرعة إرسال التحديثات.",
                "لا. الموصى به 500.",
                "إذا واجهت مشكلة مع لعبة لا تتحمل تحديثات سريعة، اخفضه.",
                "قيمة منخفضة جداً تؤخر وصول الأزرار." },
            new[] { "Max updates to the game per second",
                "The most times per second the app sends a new state to the game. It only sends when something changes.",
                "Limits how fast updates are sent.",
                "No. Recommended: 500.",
                "If a game has trouble with fast updates, lower it.",
                "A very low value delays your buttons." });

        T("preset",
            new[] { "إعدادات التوقيت الجاهزة",
                "مجموعات جاهزة لإعدادات السرعة والتوقيت.",
                "تضبط عدد القراءات والتحديثات (وأحياناً مدة المساعدات) دفعة واحدة.",
                "لا. «متوازن» هو الموصى به.",
                "جرّب «استجابة أسرع» إذا كان حاسوبك قوياً.",
                "لا يوجد خطر، ويمكن الرجوع لـ «متوازن» في أي وقت." },
            new[] { "Timing presets",
                "Ready-made speed and timing settings.",
                "Sets the read rate and update rate (and sometimes helper times) at once.",
                "No. “Balanced” is recommended.",
                "Try “Faster response” if your PC is powerful.",
                "No risk; you can go back to “Balanced” any time." });

        T("dryrun",
            new[] { "وضع التجربة (بدون يد افتراضية)",
                "يُجري البرنامج كل الحسابات ويعرضها لك، لكنه لا ينشئ يداً افتراضية ولا يرسل شيئاً للعبة.",
                "ترى الشاشة تعمل، لكن اللعبة لا تتأثر.",
                "لا. مفيد للتجربة فقط.",
                "لتجربة الإعدادات قبل تشغيل اللعبة.",
                "إن نسيت أنه مفعّل فلن تعمل اليد داخل اللعبة." },
            new[] { "Test mode (no virtual controller)",
                "The app does all the calculations and shows them, but creates no virtual controller and sends nothing to the game.",
                "You see the screen working but the game is not affected.",
                "No. Only useful for testing.",
                "To try settings before starting the game.",
                "If you forget it is on, the controller will not work in the game." });

        T("hotkeys",
            new[] { "اختصارات لوحة المفاتيح",
                "اختصارات تعمل حتى والبرنامج في الخلفية: Ctrl+Alt+F10 إيقاف طارئ، وF9 تشغيل/إيقاف التحويل، وF8 تشغيل/إيقاف تحويل الأنالوق.",
                "تتحكم بالبرنامج دون الخروج من اللعبة.",
                "موصى بها.",
                "للإيقاف الطارئ السريع.",
                "قد تتعارض مع برنامج آخر يستخدم نفس الاختصار." },
            new[] { "Keyboard shortcuts",
                "Shortcuts that work even with the app in the background: Ctrl+Alt+F10 emergency stop, F9 translation on/off, F8 analog conversion on/off.",
                "Control the app without leaving the game.",
                "Recommended.",
                "For a quick emergency stop.",
                "May clash with another program using the same shortcut." });

        T("remap",
            new[] { "إعادة تعيين الأزرار",
                "اختر لكل زر في يدك الحقيقية الزر الذي تراه اللعبة.",
                "الزر المحدد يتحول إلى الزر الجديد. «لا شيء» يُخفي الزر عن اللعبة.",
                "لا. الإعداد الافتراضي جاهز.",
                "تريد أن يعمل زر لوحة اللمس كزر آخر.",
                "تغيير الأزرار الأساسية قد يربكك. يمكنك دائماً الرجوع للافتراضي." },
            new[] { "Button mapping",
                "For each button on your real controller, choose the button the game sees.",
                "The chosen button becomes the new one. “None” hides the button from the game.",
                "No. The default is ready.",
                "You want the touchpad click to act as another button.",
                "Changing the main buttons may confuse you. You can always go back to the default." });

        T("profiles",
            new[] { "ملفات الإعدادات",
                "ملف يحفظ كل إعداداتك (الأزرار والأنالوق والمساعدات) باسم تختاره.",
                "تبدّل بين الملفات بنقرة. كل ملف مستقل عن الآخر.",
                "لا. ملف واحد يكفي.",
                "ملف للأسهم فقط وملف آخر للأنالوق.",
                "حذف الملف نهائي، لذلك يطلب البرنامج تأكيدك." },
            new[] { "Settings profiles",
                "A file that stores all your settings (buttons, analog, helpers) under a name you choose.",
                "Switch between profiles with one click. Each is independent.",
                "No. One profile is enough.",
                "One profile for arrows only and another for the stick.",
                "Deleting a profile is permanent, so the app asks you to confirm." });

        T("devmode",
            new[] { "وضع المطور",
                "صفحة معلومات تقنية: حالة التعريف، قراءات خام، سجل الأخطاء.",
                "تظهر صفحة إضافية في القائمة.",
                "لا. للمستخدمين المتقدمين أو عند طلب الدعم.",
                "عندما تريد معرفة سبب عدم ظهور اليد الافتراضية.",
                "معلومات كثيرة قد تكون مربكة. لا تغيّر فيها شيئاً إن لم تكن متأكداً." },
            new[] { "Developer mode",
                "A technical information page: driver status, raw readings, error log.",
                "An extra page appears in the menu.",
                "No. For advanced users or when asking for support.",
                "When you want to find out why the virtual controller does not appear.",
                "Lots of information can be confusing. Don't change anything there unless sure." });

        T("language",
            new[] { "لغة البرنامج",
                "اختر لغة واجهة البرنامج: العربية أو الإنجليزية.",
                "تتغير النصوص واتجاه الواجهة فوراً.",
                "اختر ما يناسبك.",
                "إذا أردت واجهة بالإنجليزية اضغط English.",
                "لا." },
            new[] { "App language",
                "Choose the interface language: Arabic or English.",
                "Texts and layout direction change immediately.",
                "Pick what suits you.",
                "If you want an Arabic interface press العربية.",
                "No." });

        T("restore",
            new[] { "استعادة الإعدادات الافتراضية",
                "تعيد الملف الحالي وخيارات البرنامج إلى القيم الموصى بها.",
                "تُستبدل إعدادات الملف الحالي بالإعدادات الأصلية. ملفاتك الأخرى لا تتأثر.",
                "فقط إذا ضاعت الإعدادات أو توقفت الأمور عن العمل.",
                "جرّبت إعدادات كثيرة ولا تعرف كيف ترجع.",
                "ستفقد تعديلاتك على الملف الحالي، لذلك يطلب البرنامج تأكيدك." },
            new[] { "Restore default settings",
                "Puts the current profile and app options back to the recommended values.",
                "The current profile's settings are replaced by the originals. Your other profiles are untouched.",
                "Only if settings got messy or things stopped working.",
                "You tried many settings and don't know how to go back.",
                "You lose your edits to the current profile, so the app asks you to confirm." });
    }

    static readonly (string q, string a)[] FaqAr =
    {
        ("ما الفرق بين Physical Controller وVirtual Controller؟",
         "الـ Physical Controller هي يدك الحقيقية التي تمسكها بيدك. أما الـ Virtual Controller فهي يد وهمية ينشئها البرنامج داخل الحاسوب، وتراها اللعبة على أنها يد حقيقية. البرنامج يقرأ ما تفعله بيدك الحقيقية، يحوّله، ثم يرسله إلى اليد الافتراضية. الاثنتان مستقلتان: يمكنك استخدام يد Xbox وتظهر للعبة كـ DualShock 4."),
        ("لماذا أحتاج إلى Virtual DS4؟",
         "هي الاختيار الافتراضي لأنها تُظهر أزرار PlayStation في الألعاب التي تدعمها. إذا لم تتعرف عليها اللعبة اختر Xbox 360. ولا يشترط أن تكون يدك الحقيقية من PlayStation."),
        ("ما وظيفة R2 في ASTRO POLSWID؟",
         "في الإعداد الافتراضي يحوّل البرنامج ضغط R2 إلى «السهم للأسفل» في اليد الافتراضية، ولا يرسل قيمة R2 الأصلية. يبقى السهم مضغوطاً طالما تضغط R2 وينتهي بمجرد تركه. ويمكنك تغيير هذا من الإعدادات المتقدمة."),
        ("ما معنى تحويل Analog إلى D-pad؟",
         "الأنالوق عصا تعطي قيمة متدرجة، والـ D-pad أزرار الاتجاهات (تعمل بالضغط أو عدمه فقط). عند التفعيل، إذا حرّكت الأنالوق الأيسر يميناً أو يساراً بما يكفي، يضغط البرنامج السهم المناسب. وتبقى حركة الأنالوق للأعلى والأسفل كما هي."),
        ("كيف أعرف أن البرنامج يتعرف على يد التحكم؟",
         "في الشاشة الرئيسية، تظهر نقطة خضراء وعبارة «متصلة» بجانب اسم يدك. حرّك الأنالوق واضغط الأزرار: سترى الحركة في الرسم. وإذا لم تظهر، جرّب كابلاً آخر أو أعد توصيل البلوتوث، ثم افتح الإعدادات المتقدمة ← فحص الإدخال."),
        ("لماذا لا تظهر وحدة التحكم الافتراضية في اللعبة؟",
         "جرّب بالترتيب:\n١) تأكد أن الحالة في الشاشة الرئيسية تقول «اليد الافتراضية: متصلة».\n٢) إذا ظهرت مشكلة، ثبّت برنامج ViGEmBus (انظر سؤال التثبيت) ثم أعد تشغيل الحاسوب.\n٣) أغلق اللعبة وشغّل ASTRO POLSWID أولاً ثم افتح اللعبة.\n٤) في إعدادات اللعبة اختر اليد الجديدة إن كانت اللعبة تسمح بذلك.\n٥) إذا ظهرت يدان في اللعبة (الحقيقية والافتراضية)، أخفِ اليد الحقيقية عن اللعبة باستخدام HidHide."),
        ("كيف أستعيد الإعدادات الافتراضية؟",
         "افتح «الإعدادات الأساسية» ثم اضغط «استعادة الإعدادات الافتراضية» وأكّد. سيعيد البرنامج ملف الإعدادات الحالي إلى القيم الموصى بها دون حذف ملفاتك الأخرى."),
        ("كيف أثبّت ViGEmBus؟",
         "ViGEmBus برنامج مجاني ينشئ اليد الافتراضية في ويندوز. ASTRO POLSWID لا يثبّته نيابة عنك. حمّله بنفسك من الصفحة الرسمية: github.com/nefarius/ViGEmBus/releases ثم شغّل الملف كمسؤول (Run as administrator)، وأعد تشغيل الحاسوب إن طُلب منك، ثم افتح ASTRO POLSWID. المشروع متوقف رسمياً لكن آخر إصدار منه ما زال يعمل."),
        ("اللعبة ترى يدين أو الأزرار تُضغط مرتين",
         "اللعبة ترى يدك الحقيقية واليد الافتراضية معاً. الحل: استخدم برنامج HidHide لإخفاء يدك الحقيقية عن اللعبة، مع السماح لـ ASTRO POLSWID.exe برؤيتها. حمّله من github.com/nefarius/HidHide. هذا البرنامج لا يغيّر إعدادات HidHide بنفسه."),
        ("البرنامج لا يكتشف يد التحكم",
         "١) جرّب كابلاً آخر أو منفذ USB آخر.\n٢) في البلوتوث: احذف الجهاز من ويندوز وأعد إقرانه.\n٣) أغلق البرامج الأخرى التي تستخدم اليد (Steam، DS4Windows…).\n٤) افتح الإعدادات الأساسية وفعّل «وضع المطور» لقراءة التفاصيل."),
        ("هل يلعب البرنامج بدلاً مني؟",
         "لا. ASTRO POLSWID لا يضغط ولا يوجّه ولا يسرّع بدلاً منك. هو يحوّل فقط ما تفعله أنت بيدك. المساعدات الاختيارية معطّلة افتراضياً ولا تعمل إلا عندما تضغط زر تشغيلها بنفسك."),
        ("هل يضمن البرنامج نجاح تقنية Polswid؟",
         "لا. البرنامج يضمن فقط ما يرسله إلى اليد الافتراضية. هو لا يرى داخل اللعبة، فلا يعرف إن كانت التقنية تعمل. تأكد من ذلك بنفسك داخل اللعبة."),
    };

    static readonly (string q, string a)[] FaqEn =
    {
        ("What is the difference between a Physical and a Virtual controller?",
         "The physical controller is the one in your hands. The virtual controller is a pretend controller the app creates inside the PC, which the game sees as a real one. The app reads your real controller, translates it, and sends it to the virtual one. They are independent: an Xbox pad can appear to the game as a DualShock 4."),
        ("Why would I need a Virtual DS4?",
         "You do not always need it. It is the default because it shows PlayStation buttons. If the game does not recognise it, choose Xbox 360. Your real controller does not need to be a PlayStation one."),
        ("What does R2 do in ASTRO POLSWID?",
         "By default the app turns an R2 press into “D-Pad Down” on the virtual controller and does not send R2's original value. It stays pressed while you hold R2 and ends when you let go. You can change this in Advanced settings."),
        ("What does converting Analog to D-pad mean?",
         "A stick gives a gradual value; the D-pad is buttons (pressed or not). When enabled, pushing the left stick far enough left or right makes the app press the matching arrow. Up/down stick movement stays unchanged."),
        ("How do I know the app recognises my controller?",
         "On the home screen a green dot and “Connected” appear next to its name. Move the sticks and press buttons: you'll see them in the drawing. If not, try another cable or reconnect Bluetooth, then open Advanced settings → Input check."),
        ("Why doesn't the virtual controller appear in the game?",
         "Try in order:\n1) Make sure the home screen says the virtual controller is connected.\n2) If there is a problem, install ViGEmBus (see the installation question) and restart the PC.\n3) Close the game, start ASTRO POLSWID first, then open the game.\n4) In the game's settings choose the new controller if it lets you.\n5) If the game shows two controllers (real and virtual), hide the real one from the game with HidHide."),
        ("How do I restore the default settings?",
         "Open “Basic settings”, press “Restore default settings” and confirm. The current profile returns to the recommended values; your other profiles are not deleted."),
        ("How do I install ViGEmBus?",
         "ViGEmBus is a free driver that creates the virtual controller in Windows. ASTRO POLSWID does not install it for you. Download it yourself from github.com/nefarius/ViGEmBus/releases, run it as administrator, restart the PC if asked, then open ASTRO POLSWID. The project is retired but its last release still works."),
        ("The game sees two controllers or buttons press twice",
         "The game sees both your real and the virtual controller. Fix: use HidHide to hide your real controller from the game while allowing ASTRO POLSWID.exe to see it. Get it at github.com/nefarius/HidHide. This app does not change HidHide settings itself."),
        ("The app doesn't detect my controller",
         "1) Try another cable or USB port.\n2) Bluetooth: remove the device in Windows and pair it again.\n3) Close other programs using the controller (Steam, DS4Windows…).\n4) Open Basic settings and turn on Developer mode to read the details."),
        ("Does the app play for me?",
         "No. ASTRO POLSWID never presses, steers or accelerates for you. It only translates what you do. The optional helpers are off by default and run only when you press their trigger yourself."),
        ("Does the app guarantee that Polswid Tech works?",
         "No. It only guarantees what it sends to the virtual controller. It cannot see inside the game, so it doesn't know whether the technique works. Verify that yourself in the game."),
    };
}
