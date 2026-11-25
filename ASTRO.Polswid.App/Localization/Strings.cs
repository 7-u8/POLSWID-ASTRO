namespace ASTRO.Polswid.App.Localization;

/// <summary>All interface strings (Arabic first, English second). Technical names (ViGEmBus, SDL3, HidHide, DS4...) stay in English.</summary>
public static class Strings
{
    static readonly Dictionary<string, (string ar, string en)> D = new();
    static void A(string key, string ar, string en) => D[key] = (ar, en);

    public static string Get(string key, Lang l) =>
        D.TryGetValue(key, out var v) ? (l == Lang.Ar ? v.ar : v.en) : key;

    public static bool Has(string key) => D.ContainsKey(key);
    public static IEnumerable<(string key, string ar, string en)> All => D.Select(kv => (kv.Key, kv.Value.ar, kv.Value.en));

    static Strings()
    {
        // ---------- navigation / shell
        A("nav_home", "الرئيسية", "Home");
        A("nav_advanced", "الإعدادات المتقدمة", "Advanced settings");
        A("nav_help", "المساعدة", "Help");
        A("nav_dev", "وضع المطور", "Developer mode");
        A("nav_settings", "الإعدادات الأساسية", "Basic settings");
        A("lang_toggle", "English", "العربية");
        A("tip_lang", "تغيير لغة البرنامج", "Change the app language");
        A("btn_emergency", "إيقاف طارئ", "Emergency stop");
        A("btn_clear_emergency", "إلغاء الإيقاف الطارئ", "Clear emergency stop");
        A("tip_emergency", "يوقف التحويل فوراً ويرسل للعبة حركاتك الأصلية كما هي. الاختصار: Ctrl+Alt+F10", "Stops the translation at once and sends your raw input to the game. Shortcut: Ctrl+Alt+F10");
        A("btn_retry", "إعادة المحاولة", "Try again");
        A("btn_ok", "حسناً", "OK");
        A("lbl_none_dash", "–", "–");
        A("dlg_title", "ASTRO POLSWID", "ASTRO POLSWID");
        A("dlg_yes", "نعم", "Yes");
        A("dlg_no", "لا", "No");

        // ---------- home (level 1)
        A("home_title", "أهلاً بك في ASTRO POLSWID", "Welcome to ASTRO POLSWID");
        A("home_sub", "وصّل يد التحكم، اختر اليد الافتراضية، ثم شغّل البرنامج. هذا كل شيء.", "Connect your controller, pick the virtual controller, then turn the app on. That's all.");
        A("home_master", "تفعيل ASTRO POLSWID", "Turn ASTRO POLSWID on");
        A("home_master_on", "يعمل الآن: يتم تحويل أزرارك.", "Running: your buttons are being translated.");
        A("home_master_off", "متوقف: تصل أزرارك للعبة كما هي.", "Off: your buttons reach the game unchanged.");
        A("home_physical_title", "يد التحكم الحقيقية", "Your real controller");
        A("refresh_controllers", "تحديث البحث عن أيدي التحكم", "Refresh Controllers");
        A("hidhide_missing_hint", "قد تكون اليد مخفية بواسطة HidHide، أو غير موصّلة، أو غير مدعومة في SDL. راجع إرشادات HidHide أدناه ثم حدّث البحث. لا يستطيع البرنامج تأكيد سبب عدم ظهورها.", "The controller may be hidden by HidHide, disconnected, or unsupported by SDL. Check the HidHide guidance below, then refresh. The app cannot confirm why it is missing.");
        A("hidhide_setup_title", "إرشادات HidHide (اختيارية)", "HidHide setup guidance (optional)");
        A("hidhide_unverified", "اضغط فحص HidHide لقراءة الإعدادات دون تغييرها. منح الإذن لهذه النسخة يتطلب تأكيدك. يجب تشغيل المحرك لتحديث البحث عن أيدي التحكم.", "Click Check HidHide to read configuration without changing it. Granting this executable access requires your confirmation. Start the engine to refresh controller discovery.");
        A("hidhide_reader_path", "المسار الفعلي للبرنامج الذي يقرأ اليد:", "Exact running executable that reads the controller:");
        A("hidhide_copy_path", "نسخ مسار البرنامج", "Copy executable path");
        A("hidhide_check", "فحص HidHide", "Check HidHide");
        A("hidhide_allow_this_app", "السماح لهذه النسخة فقط…", "Allow this executable only…");
        A("hidhide_checking", "جارٍ قراءة إعدادات HidHide…", "Reading HidHide configuration…");
        A("hidhide_read_error", "تعذّر فحص HidHide. أغلق نافذة HidHide Configuration Client ثم أعد الفحص. لم يتم التحقق من الإذن.", "Cannot read HidHide. Close HidHide Configuration Client and retry. Access is unverified.");
        A("hidhide_not_installed", "واجهة تعريف HidHide غير متاحة. لم يتم تغيير أي إعداد.", "The HidHide driver interface is not available. No configuration was changed.");
        A("hidhide_access_allowed", "سياسة HidHide تسمح لمسار هذه النسخة؛ يجب التحقق من قراءة اليد عبر SDL.", "HidHide policy permits this executable path; actual SDL input still needs verification.");
        A("hidhide_access_denied", "سياسة HidHide تمنع هذه النسخة من قراءة الأجهزة المخفية. يمكنك السماح لهذه النسخة فقط بعد التأكيد.", "HidHide policy blocks this executable from hidden devices. You can allow only this executable after confirmation.");
        A("hidhide_state", "الإخفاء مفعّل: {0} | القائمة معكوسة: {1}\nإدخالات DualSense Edge في قائمة الإخفاء: {2} (ليست دليلاً على الاتصال الحالي).", "Hiding enabled: {0} | Inverse list: {1}\nDualSense Edge entries in the hidden list: {2} (not proof of current connection).");
        A("hidhide_confirm_allow", "هل تسمح بإضافة هذا المسار فقط إلى قائمة تطبيقات HidHide؟\n{0}\nستبقى اليد مخفية عن اللعبة. لن يتم تعطيل الإخفاء أو تعديل الأجهزة المخفية أو أذونات التطبيقات الأخرى. يتطلب ذلك الوضع العادي للقائمة مع تفعيل الإخفاء.", "Allow adding only this executable path to HidHide's application list?\n{0}\nThe controller stays hidden from the game. Hiding, hidden devices, and other applications are not intentionally changed. Normal allow-list mode with hiding enabled is required.");
        A("hidhide_restart_after_allow", "أعد تشغيل ASTRO POLSWID بعد منح الإذن إذا لم تظهر اليد. اترك HidHide مفعّلاً.", "Restart ASTRO POLSWID after granting access if the pad is still absent. Keep HidHide enabled.");
        A("hidhide_setup_steps", "1. افتح HidHide Configuration Client ثم Applications.\n2. أضف البرنامج الموجود في المسار أعلاه إلى قائمة التطبيقات المسموح لها بالوصول (الوضع العادي، دون عكس القائمة). النسخ في مجلدات مختلفة تحتاج مساراتها الخاصة.\n3. اترك اليد الحقيقية محددة في Devices، واترك Enable device hiding مفعّلاً.\n4. اترك اليد الافتراضية ظاهرة للعبة، ولا تمنح اللعبة أو المحاكي وصولاً مباشراً إلى اليد الحقيقية.\n5. عد إلى ASTRO POLSWID واضغط تحديث البحث. إذا لم تظهر اليد بعد منح الإذن، أعد تشغيل التطبيق.\nيستخدم التطبيق SDL داخل هذا البرنامج نفسه؛ لا يوجد برنامج مساعد منفصل لقراءة اليد. لا تعطّل HidHide لإجراء هذا الاختبار.", "1. Open HidHide Configuration Client, then Applications.\n2. Add the executable at the exact path above to the allowed applications (normal allow-list mode, without inverse cloaking). Builds in different folders need their own paths.\n3. Keep the physical controller selected in Devices and keep Enable device hiding enabled.\n4. Leave the virtual controller visible to the game. Do not give the game or emulator direct access to the hidden physical controller.\n5. Return here and click Refresh Controllers. If access was just granted and the pad is still absent, restart ASTRO POLSWID.\nSDL reads input inside this executable; there is no separate input helper. Keep HidHide enabled during verification.");
        A("home_virtual_title", "وحدة التحكم الافتراضية (ما ستراه اللعبة)", "Virtual controller (what the game sees)");
        A("home_live_title", "ما يحدث الآن", "What is happening now");
        A("home_direction", "الاتجاه الحالي", "Current direction");
        A("home_r2", "زر R2 ← السهم للأسفل", "R2 button → D-Pad Down");
        A("home_vr2_label", "إشارة R2 للعبة", "R2 signal to game");
        A("home_vr2_suppressed", "محجوبة تماماً (0%)", "Suppressed (0%)");
        A("home_polswid_label", "وضع Polswid", "Polswid mode");
        A("home_view_virt", "ما يصل للعبة (افتراضية)", "Sent to Game (Virtual)");
        A("home_view_phys", "يدك الحقيقية (أصلية)", "Real Controller (Physical)");
        A("home_game_hint", "💡 تنبيه اللعبة: اختر في إعدادات اللعبة أو المحاكي اليد الافتراضية (DualShock 4 أو Xbox 360) لتمنع اللعبة من التقاط R2 الحقيقي من الويندوز.", "💡 Game tip: In your game or emulator settings, select the Virtual Controller (DualShock 4 or Xbox 360) to prevent the game from receiving physical R2 from Windows.");
        A("r2_pressed", "مضغوط", "pressed");
        A("r2_released", "غير مضغوط", "not pressed");
        A("home_basic_settings", "الإعدادات الأساسية", "Basic settings");
        A("home_status_title", "حالة الاتصال", "Connection status");
        A("home_status_phys", "اليد الحقيقية", "Real controller");
        A("home_status_virt", "اليد الافتراضية", "Virtual controller");
        A("home_note", "البرنامج لا يضغط أي زر بدلاً منك. هو فقط يحوّل ما تضغطه أنت.", "The app never presses buttons for you. It only translates what you press.");
        A("dir_left", "يسار", "Left"); A("dir_right", "يمين", "Right"); A("dir_neutral", "لا اتجاه", "None"); A("dir_both", "الاثنان (تجريبي)", "Both (experimental)");
        A("owner_none", "لا أحد", "No one"); A("owner_analog", "الأنالوق الأيسر", "Left analog"); A("owner_dpad", "الأسهم", "D-Pad");

        // ---------- status texts
        A("st_stopped", "المحرك متوقف", "Engine stopped");
        A("st_phys_ok", "متصلة: {0}", "Connected: {0}");
        A("st_phys_none", "لم يتم العثور على يد تحكم", "No controller found");
        A("st_virt_ok", "متصلة: {0}", "Connected: {0}");
        A("st_virt_dry", "وضع التجربة: لا توجد يد افتراضية", "Test mode: no virtual controller");
        A("st_virt_err", "غير متصلة: هناك مشكلة", "Not connected: there is a problem");
        A("st_virt_off", "غير متصلة", "Not connected");
        A("banner_stopped", "المحرك متوقف ولا يتم إرسال شيء للعبة.", "The engine is stopped and nothing is sent to the game.");
        A("banner_emergency", "الإيقاف الطارئ مفعّل: تصل أزرارك للعبة كما هي. ألغِ الإيقاف ثم فعّل التحويل.", "Emergency stop is on: your buttons reach the game unchanged. Clear it, then turn translation on.");
        A("banner_wait_phys", "بانتظار يد التحكم… وصّلها بكابل أو بلوتوث.", "Waiting for a controller… connect it by cable or Bluetooth.");
        A("banner_not_fwd", "لا يتم إرسال شيء للعبة: اليد الافتراضية غير متاحة.", "Nothing is sent to the game: the virtual controller is unavailable.");
        A("banner_dry", "وضع التجربة: كل شيء يُحسب ويُعرض هنا لكن لا يصل للعبة.", "Test mode: everything is computed and shown here but nothing reaches the game.");
        A("banner_off", "التحويل متوقف: تصل أزرارك للعبة كما هي.", "Translation is off: your buttons reach the game unchanged.");
        A("banner_on", "كل شيء يعمل. تأكد داخل اللعبة أنها ترى اليد الافتراضية.", "All good. Check inside the game that it sees the virtual controller.");
        A("err_driver_missing", "برنامج ViGEmBus غير مثبّت أو لا يعمل. هو المسؤول عن إنشاء اليد الافتراضية. ثبّته ثم اضغط «إعادة المحاولة». الخطوات في صفحة المساعدة.", "ViGEmBus is not installed or not running. It creates the virtual controller. Install it, then press “Try again”. Steps are in the Help page.");
        A("err_virtual_generic", "تعذّر إنشاء اليد الافتراضية. راجع صفحة المساعدة، أو افتح وضع المطور لمعرفة التفاصيل.", "The virtual controller could not be created. See the Help page, or open Developer mode for details.");
        A("err_ds4_hint", "إذا استمرت المشكلة مع DualShock 4 يمكنك اختيار Xbox 360 بنفسك. لن يغيّره البرنامج من تلقاء نفسه.", "If DualShock 4 keeps failing you can choose Xbox 360 yourself. The app will not change it on its own.");
        A("drv_ok", "برنامج ViGEmBus جاهز.", "ViGEmBus is ready.");
        A("drv_ok_ver", "برنامج ViGEmBus جاهز (الإصدار {0}).", "ViGEmBus is ready (version {0}).");
        A("drv_unreachable", "ViGEmBus مثبّت لكن لا يمكن فتحه. أعد تشغيل الحاسوب أو أعد تثبيته.", "ViGEmBus is installed but cannot be opened. Restart the PC or reinstall it.");
        A("drv_missing", "ViGEmBus غير مثبّت. هذا البرنامج لا يثبّت التعريفات تلقائياً. راجع صفحة المساعدة.", "ViGEmBus is not installed. This app never installs drivers by itself. See the Help page.");

        // ---------- controller panels
        A("device_auto", "تلقائي (أول يد تُكتشف ثم يبقى عليها)", "Automatic (first controller found, then kept)");
        A("device_not_connected", "{0} (غير متصلة)", "{0} (not connected)");
        A("lbl_type", "النوع", "Type"); A("lbl_connection", "طريقة الاتصال", "Connection"); A("lbl_battery", "البطارية", "Battery");
        A("lbl_api", "واجهة الإدخال", "Input API"); A("lbl_caps", "الإمكانات", "Capabilities");
        A("out_x360", "Xbox 360", "Xbox 360");
        A("out_ds4", "DualShock 4 (PlayStation)", "DualShock 4 (PlayStation)");
        A("out_x360_desc", "الأكثر توافقاً مع أغلب الألعاب.", "Works with most games.");
        A("out_ds4_desc", "تظهر أزرار PlayStation في الألعاب التي تدعمها.", "Shows PlayStation buttons in games that support it.");
        A("out_note_x360", "لوحة اللمس غير موجودة في Xbox 360 فلا تُرسل. زر Share يصبح Back وزر Options يصبح Start.", "Xbox 360 has no touchpad so it is not sent. Share becomes Back and Options becomes Start.");
        A("out_note_ds4", "يتم إرسال أزرار Share وOptions وPS ولوحة اللمس.", "Share, Options, PS and touchpad click are sent.");
        A("out_independent", "هذا الاختيار مستقل تماماً عن نوع يدك الحقيقية.", "This choice is completely independent of your real controller's brand.");
        A("conn_wired", "كابل USB", "USB cable"); A("conn_wireless", "بلوتوث / لاسلكي", "Bluetooth / wireless"); A("conn_unknown", "غير معروف", "Unknown");
        A("bat_wired", "يعمل بالكابل", "Wired (no battery)"); A("bat_unknown", "غير متاحة", "Not reported"); A("bat_charged", "ممتلئة", "Charged");
        A("bat_pct", "{0}%", "{0}%"); A("bat_charging", "{0}% (قيد الشحن)", "{0}% (charging)");
        A("api_sdl", "SDL3 (يختار HIDAPI أو XInput تلقائياً)", "SDL3 (HIDAPI / XInput chosen automatically)");
        A("cap_buttons", "أزرار", "buttons"); A("cap_dpad", "أسهم", "D-Pad"); A("cap_sticks", "أنالوقان", "2 sticks"); A("cap_triggers", "زنادان", "analog triggers");
        A("cap_touch", "لوحة لمس", "touchpad"); A("cap_gyro", "جيروسكوب (غير مستخدم)", "gyro (not used)");

        // ---------- buttons (PlayStation naming with Xbox equivalents)
        A("btn_None", "لا شيء (إخفاء الزر)", "None (hide the button)");
        A("btn_Cross", "زر X (Cross / A)", "Cross (X / Xbox A)");
        A("btn_Circle", "زر الدائرة (Circle / B)", "Circle (Xbox B)");
        A("btn_Square", "زر المربع (Square / X)", "Square (Xbox X)");
        A("btn_Triangle", "زر المثلث (Triangle / Y)", "Triangle (Xbox Y)");
        A("btn_L1", "الكتف الأيسر L1", "Left bumper L1"); A("btn_R1", "الكتف الأيمن R1", "Right bumper R1");
        A("btn_L2", "الزناد الأيسر L2", "Left trigger L2"); A("btn_R2", "الزناد الأيمن R2", "Right trigger R2");
        A("btn_Share", "زر Share / Back", "Share / Back / View"); A("btn_Options", "زر Options / Start", "Options / Start / Menu");
        A("btn_L3", "ضغط الأنالوق الأيسر (L3)", "Left stick click (L3)"); A("btn_R3", "ضغط الأنالوق الأيمن (R3)", "Right stick click (R3)");
        A("btn_Guide", "زر PS / Guide", "PS / Guide"); A("btn_Touchpad", "لوحة اللمس (Touchpad)", "Touchpad click");
        A("btn_DpadUp", "السهم للأعلى", "D-Pad Up"); A("btn_DpadDown", "السهم للأسفل", "D-Pad Down");
        A("btn_DpadLeft", "السهم لليسار", "D-Pad Left"); A("btn_DpadRight", "السهم لليمين", "D-Pad Right");

        // ---------- enums
        A("enum_PriorityMode_AnalogFirst", "الأنالوق أولاً (موصى به)", "Analog first (recommended)");
        A("enum_PriorityMode_DPadFirst", "الأسهم أولاً", "D-Pad first");
        A("enum_PriorityMode_LastInputWins", "آخر ما استخدمته", "Whichever you touched last");
        A("enum_TriggerMode_Hold", "ضغط مستمر (موصى به)", "Hold (recommended)");
        A("enum_TriggerMode_Toggle", "تبديل (ضغطة تشغّل وضغطة تطفئ)", "Toggle (press on, press off)");
        A("enum_UTurnExecution_Manual", "يدوي (طالما تضغط الزر)", "Manual (while you hold the button)");
        A("enum_UTurnExecution_Assisted", "مساعد (ضغطة واحدة تكفي)", "Assisted (one press)");
        A("enum_VirtualPadType_Xbox360", "Xbox 360", "Xbox 360");
        A("enum_VirtualPadType_DualShock4", "DualShock 4 (PlayStation)", "DualShock 4 (PlayStation)");
        A("preset_Balanced", "متوازن (موصى به)", "Balanced (recommended)");
        A("preset_Low_latency", "استجابة أسرع", "Faster response");
        A("preset_Frame_locked_60_fps", "مناسب لـ 60 إطاراً", "Matched to 60 fps");
        A("preset_Frame_locked_30_fps", "مناسب لـ 30 إطاراً", "Matched to 30 fps");
        A("preset_Custom", "مخصص", "Custom");

        // ---------- badges / defaults
        A("badge_recommended", "موصى به", "Recommended");
        A("badge_optional", "اختياري", "Optional");
        A("badge_advanced", "للمتقدمين", "Advanced");
        A("rec_value", "الموصى به: {0}", "Recommended: {0}");
        A("tip_use_rec", "ارجع إلى القيمة الموصى بها", "Go back to the recommended value");
        A("tip_help", "اضغط لشرح بسيط مع مثال", "Click for a simple explanation with an example");
        A("hp_what", "ما هذه الخاصية؟", "What is it?");
        A("hp_effect", "ماذا يحدث عند تفعيلها؟", "What happens when it is on?");
        A("hp_need", "هل أحتاجها؟", "Do I need it?");
        A("hp_example", "متى أستخدمها؟", "When would I use it?");
        A("hp_risk", "هل يمكن أن تسبب مشكلة؟", "Can it cause problems?");

        // ---------- advanced shell + tabs
        A("adv_title", "الإعدادات المتقدمة", "Advanced settings");
        A("adv_sub", "لا تحتاج هذه الصفحة للاستخدام العادي. بجانب كل خاصية زر «؟» يشرحها بلغة بسيطة، والقيمة الموصى بها مكتوبة بجانبها.", "You don't need this page for normal use. Every setting has a “?” button with a plain explanation, and the recommended value is shown next to it.");
        A("tab_buttons", "الأزرار", "Buttons");
        A("tab_steering", "الأنالوق والاتجاه", "Analog & direction");
        A("tab_helpers", "المساعدات", "Helpers");
        A("tab_profiles", "الملفات والتوقيت", "Profiles & timing");
        A("tab_diag", "فحص الإدخال", "Input check");

        // ---------- mapping page
        A("map_title", "إعادة تعيين الأزرار", "Button mapping");
        A("map_hint", "اضغط على أي زر في الرسم لتغيير وظيفته. أسماء الأزرار بنمط PlayStation لكل أنواع الأيدي (X = A في Xbox).", "Click any button in the drawing to change what it does. Names follow PlayStation style for all controllers (Cross = Xbox A).");
        A("map_live", "يدك الحقيقية الآن", "Your real controller now");
        A("map_pick", "اضغط على زر في الرسم", "Click a button in the drawing");
        A("map_list_title", "الزر الحقيقي ← ما تراه اللعبة", "Real button → what the game sees");
        A("map_note", "R2 يتحول إلى السهم للأسفل ولا تُرسل قيمته الأصلية. اختر «لا شيء» لإخفاء أي زر عن اللعبة. لا يتغير أي تعيين إلا بقرارك.", "R2 becomes D-Pad Down and its analog value is not sent. Choose “None” to hide a button from the game. No mapping changes unless you change it.");

        // ---------- steering tab
        A("sec_r2", "زر R2", "R2 button");
        A("sec_analog", "الأنالوق الأيسر", "Left analog stick");
        A("sec_direction", "من يقود الاتجاه؟", "Who controls steering?");
        A("lbl_r2_mode", "سلوك R2", "R2 behaviour");
        A("lbl_passthrough", "إرسال قيمة R2 الأصلية أيضاً", "Also send the original R2 value");
        A("lbl_press_th", "حد ضغط الزناد", "Trigger press level");
        A("lbl_release_th", "حد إفلات الزناد", "Trigger release level");
        A("lbl_analog_enabled", "تحويل الأنالوق الأيسر إلى أزرار الاتجاهات", "Left analog → direction buttons");
        A("lbl_suppress", "إخفاء حركة الأنالوق الأصلية عن اللعبة", "Hide the original analog movement from the game");
        A("lbl_invert", "عكس الاتجاه (يمين ↔ يسار)", "Swap left and right");
        A("lbl_deadzone", "المنطقة الميتة", "Dead zone");
        A("lbl_activation", "حد التفعيل", "Activation level");
        A("lbl_release", "حد الإفلات", "Release level");
        A("lbl_sens", "الحساسية", "Sensitivity");
        A("lbl_curve", "منحنى الاستجابة", "Response curve");
        A("lbl_smooth", "التنعيم", "Smoothing");
        A("lbl_priority", "الأولوية", "Priority");
        A("lbl_contra", "السماح بيمين ويسار معاً (تجريبي)", "Allow left and right together (experimental)");
        A("lbl_dwell", "أقل مدة قبل تغيير الاتجاه (ميلي ثانية)", "Minimum time before direction change (ms)");
        A("hint_deadzone", "المساحة التي يُتجاهل فيها الأنالوق", "The area around the centre that is ignored");
        A("hint_activation", "كم يجب دفع الأنالوق ليُحسب ضغطة", "How far to push before it counts as a press");
        A("hint_release", "يجب أن يكون أقل من حد التفعيل", "Must stay below the activation level");
        A("hint_curve", "1 = مباشر", "1 = direct");
        A("hint_smooth", "0 = متوقف", "0 = off");
        A("hint_dwell", "0 = تغيير فوري", "0 = change immediately");
        A("steer_note", "يتغير الاتجاه دائماً بتحديث واحد كامل دون لحظة «لا اتجاه» بينهما. ولا يُرسل يمين ويسار معاً إلا في الوضع التجريبي.", "Direction always changes in one complete update with no “no direction” moment in between. Left and right are never sent together except in the experimental mode.");

        // ---------- continuous steering
        A("sec_continuous", "التوجيه المستمر مع R2", "Continuous steering with R2");
        A("lbl_cont_enable", "تفعيل التوجيه المستمر (يعمل فقط أثناء ضغط R2)", "Turn on continuous steering (works only while R2 is held)");
        A("lbl_cont_delay", "مهلة تفعيل المربع بعد ضغط R2 (ميلي ثانية)", "Delay before Square turns on after R2 (ms)");
        A("lbl_cont_fallback", "الاتجاه عند ثبات الأنالوق الأيسر", "Direction while the left stick is still");
        A("hint_cont_delay", "يُضغط المربع تلقائياً بعد هذه المدة ويبقى ما دمت ضاغطاً على R2", "Square is pressed after this time and stays while R2 is held");
        A("cont_explain", "ميزة التوجيه المستمر تعمل فقط أثناء ضغطك على R2. إذا كان الأنالوق ثابتاً، يرسل البرنامج السهم الأيسر تلقائياً. عند تحريك الأنالوق، يتغير الاتجاه حسب حركتك. بمجرد ترك R2، يعود التحكم إلى وضعه الطبيعي.", "Continuous steering works only while you hold R2. If the stick is still, the app sends D-Pad Left automatically. When you move the stick, the direction follows your movement. As soon as you release R2, control returns to normal.");
        A("cont_left_only", "يستخدم الأنالوق الأيسر فقط. الأنالوق الأيمن لا يتأثر أبداً ويصل للعبة كما هو.", "Uses the LEFT stick only. The right stick is never touched and passes through as it is.");
        A("enum_SteeringDirection_Left", "يسار (موصى به)", "Left (recommended)");
        A("enum_SteeringDirection_Right", "يمين", "Right");
        A("owner_continuous", "التوجيه المستمر (R2)", "Continuous steering (R2)");
        A("row_cont", "التوجيه المستمر", "Continuous steering");
        A("v_active", "نشط", "active"); A("v_inactive", "غير نشط", "not active");

        // ---------- helpers tab
        A("helpers_note", "هذه المساعدات معطّلة افتراضياً ولا تعمل أبداً من تلقاء نفسها. كل واحدة تعمل فقط عندما تضغط أنت زر تشغيلها.", "These helpers are off by default and never run by themselves. Each one runs only when you press its trigger.");
        A("sec_uturn", "مساعد الدوران الأرضي (U-Turn)", "Grounded U-Turn helper");
        A("sec_air", "مساعد التبديل في الهواء", "Air-switch helper");
        A("sec_safety", "الأمان", "Safety");
        A("lbl_uturn_enable", "تفعيل مساعد U-Turn", "Enable U-Turn helper");
        A("lbl_execution", "طريقة التنفيذ", "How it runs");
        A("lbl_trigger_btn", "زر التشغيل", "Trigger button");
        A("lbl_release_ctrl", "الزر الذي يُرفع", "Button that is released");
        A("lbl_release_ms", "مدة الرفع (ميلي ثانية)", "Release time (ms)");
        A("lbl_air_enable", "تفعيل مساعد التبديل في الهواء", "Enable air-switch helper");
        A("lbl_jump_btn", "زر القفز", "Jump button");
        A("lbl_jump_ms", "مدة ضغط القفز (ميلي ثانية)", "Jump press time (ms)");
        A("lbl_flip_ms", "تبديل الاتجاه بعد (ميلي ثانية)", "Flip direction after (ms)");
        A("hint_flip", "البرنامج لا يعرف متى تهبط السيارة: جرّب التوقيت", "The app cannot tell when you land: tune by testing");
        A("lbl_emerg_btn", "زر الإيقاف الطارئ", "Emergency-stop button");
        A("lbl_cancel_btn", "زر إلغاء المساعدات", "Cancel-helpers button");
        A("lbl_consume", "إخفاء أزرار المساعدات عن اللعبة", "Hide helper buttons from the game");
        A("btn_run_uturn", "جرّب U-Turn الآن", "Run U-Turn now");
        A("btn_run_air", "جرّب التبديل في الهواء الآن", "Run air switch now");
        A("btn_cancel_helpers", "إلغاء المساعدات", "Cancel helpers");

        // ---------- profiles & timing tab
        A("sec_profile", "ملف الإعدادات الحالي", "Current settings profile");
        A("lbl_profile_pick", "الملف", "Profile");
        A("lbl_profile_name", "اسم الملف", "Name");
        A("btn_new", "جديد", "New"); A("btn_dup", "نسخ", "Duplicate"); A("btn_delete", "حذف", "Delete");
        A("btn_reset_profile", "استعادة هذا الملف", "Restore this profile"); A("btn_import", "استيراد…", "Import…"); A("btn_export", "تصدير…", "Export…");
        A("profiles_note", "الملفات تُحفظ في مجلد خاص بك (%APPDATA%\\ASTRO POLSWID) وتبقى كما هي عند تحديث البرنامج.", "Profiles are stored in your own folder (%APPDATA%\\ASTRO POLSWID) and stay untouched when you update the app.");
        A("sec_output", "الإخراج", "Output");
        A("lbl_output_type", "نوع اليد الافتراضية", "Virtual controller type");
        A("lbl_dryrun", "وضع التجربة (بدون يد افتراضية)", "Test mode (no virtual controller)");
        A("lbl_hotkeys", "اختصارات لوحة المفاتيح (Ctrl+Alt+F10 / F9 / F8)", "Keyboard shortcuts (Ctrl+Alt+F10 / F9 / F8)");
        A("sec_timing", "التوقيت", "Timing");
        A("lbl_preset", "إعداد جاهز", "Preset");
        A("lbl_polling", "عدد قراءات اليد في الثانية", "Controller reads per second");
        A("lbl_submit", "أقصى عدد تحديثات للعبة في الثانية", "Max updates to the game per second");
        A("hint_polling", "كم مرة يفحص البرنامج يدك", "How often the app checks your controller");
        A("hint_submit", "تُرسل فقط عند حدوث تغيير", "Only sent when something changes");
        A("timing_note", "سرعة قراءة اليد وسرعة إرسال التحديثات وسرعة اللعبة نفسها ثلاثة أشياء مختلفة. كيف تتفاعل اللعبة مع تحديثين متقاربين لا يُعرف إلا بالتجربة داخلها.", "Reading speed, update speed and the game's own speed are three different things. How the game reacts to two close updates can only be learned by testing in the game.");
        A("preset_note", "مدة المساعدات", "Helper time");

        // ---------- input check (diagnostics)
        A("diag_title", "فحص الإدخال", "Input check");
        A("diag_sub", "الرسم الأول يعرض ما تضغطه أنت (بعد توحيد شكل كل الأيدي)، والثاني يعرض ما يُرسل فعلاً للعبة. استخدمهما للتأكد قبل اللعب.", "The first drawing shows what you press (normalised across all controllers); the second shows what is actually sent to the game. Use them to check before you play.");
        A("diag_phys", "ما تضغطه أنت", "What you press");
        A("diag_virt", "ما يُرسل للعبة", "What is sent to the game");
        A("row_r2", "ضغط R2 الحقيقي", "Physical R2 pressure");
        A("row_vr2", "R2 الافتراضي (ما تراه اللعبة)", "Virtual R2 (sent to game)");
        A("row_vdown", "السهم للأسفل (للعبة)", "D-Pad Down (to game)");
        A("row_square", "المربع (للعبة)", "Square (to game)");
        A("row_sqdelay", "حالة تأخير المربع", "Square delay status");
        A("row_cross", "X (للعبة)", "Cross (to game)");
        A("row_dpad", "الأسهم الحقيقية", "Physical D-Pad");
        A("row_vdpad", "الأسهم الافتراضية (ما تراه اللعبة)", "Virtual D-Pad (sent to game)");
        A("row_lstick", "الأنالوق الأيسر X / Y", "Left stick X / Y");
        A("row_rstick_pass", "الأنالوق الأيمن (يمر كما هو)", "Right stick (passes through)");
        A("row_conv", "الاتجاه المحوَّل", "Converted direction");
        A("row_owner", "من يقود الاتجاه", "Who controls direction");
        A("row_suppress", "إخفاء حركة X الأصلية", "Original X hidden");
        A("row_submit", "الإرسال للعبة", "Sending to game");
        A("row_latency", "زمن الاستجابة", "Response time");
        A("row_rates", "القراءة / الإرسال", "Reads / sends");
        A("v_pressed", "مضغوط", "PRESSED"); A("v_released", "غير مضغوط", "released");
        A("v_neutral", "محايد", "neutral"); A("v_yes", "نعم", "yes"); A("v_no", "لا", "no");
        A("sq_pending", "قيد الانتظار (تأخير)", "PENDING (delay)");
        A("sq_active", "مفعل ومضغوط", "ACTIVE (held)");
        A("sq_inactive", "غير مفعل", "inactive");
        A("v_analog_off", "تحويل الأنالوق متوقف", "analog conversion is off");
        A("v_send_ok", "يعمل ({0} تحديث)", "OK ({0} updates)");
        A("v_send_dry", "وضع التجربة (لا شيء يُرسل)", "test mode (nothing sent)");
        A("v_send_bad", "لا يتم الإرسال", "NOT SENDING");
        A("v_to_virtual", "← للعبة", "→ game");

        // ---------- basic settings
        A("set_title", "الإعدادات الأساسية", "Basic settings");
        A("set_sub", "إعدادات بسيطة. كل ما عداها في «الإعدادات المتقدمة».", "Simple settings. Everything else is under “Advanced settings”.");
        A("set_language", "لغة البرنامج", "App language");
        A("set_lang_ar", "العربية", "العربية"); A("set_lang_en", "English", "English");
        A("set_wizard", "إعادة تشغيل معالج الإعداد", "Run the setup guide again");
        A("set_restore", "استعادة الإعدادات الافتراضية", "Restore default settings");
        A("set_restore_desc", "يعيد ملف الإعدادات الحالي وخيارات البرنامج إلى الإعدادات الموصى بها. لا يحذف ملفاتك الأخرى.", "Resets the current profile and app options to the recommended ones. Your other profiles are not deleted.");
        A("set_dev", "إظهار وضع المطور", "Show Developer mode");
        A("set_dev_desc", "صفحة معلومات تقنية للمستخدمين المتقدمين. مخفية افتراضياً.", "A technical information page for advanced users. Hidden by default.");
        A("set_hotkeys_hint", "الاختصارات تعمل حتى والبرنامج في الخلفية.", "Shortcuts work even while the app is in the background.");
        A("set_open_advanced", "فتح الإعدادات المتقدمة", "Open advanced settings");

        // ---------- confirmations
        A("confirm_restore", "هل تريد استعادة الإعدادات الافتراضية؟ سيتم استبدال إعدادات الملف الحالي.", "Restore the default settings? The current profile will be replaced.");
        A("confirm_delete", "هل تريد حذف الملف «{0}» نهائياً؟", "Delete profile “{0}” permanently?");
        A("confirm_reset_profile", "هل تريد إرجاع الملف «{0}» إلى حالته الأصلية؟", "Restore profile “{0}” to its original state?");
        A("confirm_output", "سيُعاد إنشاء اليد الافتراضية وقد تحتاج اللعبة إلى إعادة التعرّف عليها. هل تريد المتابعة؟", "The virtual controller will be recreated and the game may need to detect it again. Continue?");
        A("confirm_dryrun", "سيُعاد تشغيل المحرك. هل تريد المتابعة؟", "The engine will restart. Continue?");
        A("confirm_dev", "وضع المطور يعرض معلومات تقنية معقدة. هل تريد إظهاره؟", "Developer mode shows complex technical information. Show it?");
        A("err_import", "تعذّر استيراد الملف: {0}", "Could not import the file: {0}");
        A("err_export", "تعذّر تصدير الملف: {0}", "Could not export the file: {0}");

        // ---------- notifications (toasts)
        A("n_phys_ok", "تم الاتصال بيد التحكم: {0}", "Controller connected: {0}");
        A("n_phys_lost", "انقطع اتصال يد التحكم. تم تحرير كل الأزرار.", "The controller disconnected. All buttons were released.");
        A("n_virt_ok", "اليد الافتراضية جاهزة.", "The virtual controller is ready.");
        A("n_virt_err", "هناك مشكلة في اليد الافتراضية.", "There is a problem with the virtual controller.");
        A("n_emergency", "تم الإيقاف الطارئ. تصل أزرارك للعبة كما هي.", "Emergency stop. Your buttons reach the game unchanged.");
        A("n_restored", "تمت استعادة الإعدادات الافتراضية.", "Default settings restored.");
        A("n_on", "تم تفعيل ASTRO POLSWID.", "ASTRO POLSWID is on.");
        A("n_off", "تم إيقاف ASTRO POLSWID.", "ASTRO POLSWID is off.");

        // ---------- developer mode
        A("dev_title", "وضع المطور", "Developer mode");
        A("dev_sub", "معلومات تقنية. لا تغيّر شيئاً هنا إذا لم تكن متأكداً. تبقى هذه التفاصيل بالإنجليزية.", "Technical information. Technical details stay in English.");
        A("dev_driver", "حالة ViGEmBus والتعريف", "ViGEmBus & driver status");
        A("dev_api", "واجهة الإدخال والأجهزة", "Input API & devices");
        A("dev_perf", "الأداء", "Performance");
        A("dev_raw", "الإدخال الخام (بعد التوحيد)", "Raw input (normalised)");
        A("dev_virtual_state", "حالة اليد الافتراضية", "Virtual controller state");
        A("dev_log", "سجل التشخيص", "Debug log");
        A("dev_errors", "تفاصيل الأخطاء", "Error details");
        A("dev_start", "تشغيل المحرك", "Start engine"); A("dev_stop", "إيقاف المحرك", "Stop engine");
        A("dev_restart", "إعادة تشغيل المحرك", "Restart engine");
        A("dev_open_logs", "فتح مجلد السجلات", "Open log folder");
        A("dev_copy_log", "نسخ", "Copy"); A("dev_clear_log", "مسح", "Clear");
        A("dev_driver_page", "صفحة تحميل ViGEmBus", "ViGEmBus download page");
        A("dev_no_error", "لا توجد أخطاء.", "No errors.");

        // ---------- help center
        A("help_title", "المساعدة", "Help");
        A("help_sub", "اشرح لي بلغة بسيطة.", "Plain explanations.");
        A("help_search", "ابحث في المساعدة…", "Search help…");
        A("help_faq", "أسئلة شائعة", "Common questions");
        A("help_settings", "شرح كل خاصية", "Every setting explained");
        A("help_none", "لا توجد نتائج. جرّب كلمات أخرى.", "No results. Try other words.");
        A("help_clear", "مسح البحث", "Clear search");
        A("help_close", "إغلاق", "Close");

        // ---------- wizard
        A("wiz_title", "الإعداد الأول", "First-time setup");
        A("wiz_step", "الخطوة {0} من {1}", "Step {0} of {1}");
        A("wiz_next", "التالي", "Next"); A("wiz_back", "السابق", "Back");
        A("wiz_skip", "تخطي الإعداد", "Skip setup");
        A("wiz_finish", "إنهاء الإعداد", "Finish setup");
        A("wiz1_title", "اكتشاف يد التحكم الحقيقية", "Find your real controller");
        A("wiz1_body", "وصّل يد التحكم بكابل أو بلوتوث. سيتعرف عليها البرنامج تلقائياً، سواء كانت PlayStation أو Xbox أو غيرها.", "Connect your controller by cable or Bluetooth. The app finds it automatically, whether it is PlayStation, Xbox or another brand.");
        A("wiz1_found", "تم العثور على: {0}", "Found: {0}");
        A("wiz1_none", "لم يتم العثور على يد تحكم بعد. وصّلها وانتظر لحظة. يمكنك المتابعة وربطها لاحقاً.", "No controller found yet. Connect it and wait a moment. You can continue and connect it later.");
        A("wiz2_title", "اختيار وحدة التحكم الافتراضية", "Choose the virtual controller");
        A("wiz2_body", "هذه اليد الوهمية هي التي ستراها اللعبة. لا علاقة لها بنوع يدك الحقيقية، فيمكنك استخدام يد Xbox لتظهر في اللعبة كـ DualShock 4 والعكس.", "This pretend controller is what the game will see. It has nothing to do with your real controller's brand: an Xbox pad can appear as a DualShock 4 and the other way round.");
        A("wiz3_title", "اختبار الأزرار والأنالوق", "Test buttons and sticks");
        A("wiz3_body", "اضغط أي زر وحرّك الأنالوقين. يجب أن ترى الحركة في الرسم.", "Press any button and move both sticks. You should see them move in the drawing.");
        A("wiz3_btn_ok", "تم اكتشاف ضغط زر ✓", "Button press detected ✓");
        A("wiz3_stick_ok", "تم اكتشاف حركة الأنالوق ✓", "Stick movement detected ✓");
        A("wiz3_wait", "بانتظار الحركة…", "Waiting…");
        A("wiz4_title", "مراجعة إعدادات Polswid الافتراضية", "Review the default Polswid settings");
        A("wiz4_body", "هذه هي التحويلات الجاهزة. لا تحتاج لتغيير شيء الآن:", "These are the ready-made translations. You don't need to change anything now:");
        A("wiz4_l1", "زر R2 ← السهم للأسفل", "R2 → D-Pad Down");
        A("wiz4_l2", "السهم لليسار ← السهم لليسار", "D-Pad Left → D-Pad Left");
        A("wiz4_l3", "السهم للأسفل ← السهم لليمين", "D-Pad Down → D-Pad Right");
        A("wiz4_l4", "الأنالوق الأيسر (يمين / يسار) ← السهم لليمين / لليسار", "Left stick (right / left) → D-Pad Right / Left");
        A("wiz4_l5", "باقي الأزرار تصل كما هي", "All other buttons stay the same");
        A("wiz4_note", "البرنامج لا يضغط أي زر بدلاً منك: أنت من يتحكم في كل شيء.", "The app never presses a button for you: you control everything.");
        A("wiz5_title", "كل شيء جاهز", "All set");
        A("wiz5_body", "هذا ملخص إعداداتك. يمكنك تغيير أي شيء لاحقاً.", "Here is a summary of your setup. You can change anything later.");
        A("wiz5_phys", "اليد الحقيقية: {0}", "Real controller: {0}");
        A("wiz5_virt", "اليد الافتراضية: {0}", "Virtual controller: {0}");
        A("wiz5_remap", "التحويل: R2 ← السهم للأسفل، والأنالوق الأيسر ← الأسهم", "Translation: R2 → D-Pad Down, left stick → arrows");
        A("wiz5_hint", "بعد الإنهاء ستجد مفتاح التشغيل الكبير في الشاشة الرئيسية.", "After finishing you'll find the big on/off switch on the home screen.");
        A("wiz_no_controller", "لا توجد يد", "No controller");
    }
}
