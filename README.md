<!-- README.md -->

<p align="center">
  <img src="https://github.com/yanivbahalul/noodles-simulator/blob/main/wwwroot/logo/noodles-logo-transparent.png" alt="Noodles Simulator Logo" width="300">
</p>

<h1 align="center">🍜 Noodles Simulator</h1>

<p align="center">
  The ultimate interactive simulator for practicing OOP questions – now live and always up to date!
</p>

---

## 🌐 Live Version

🔗 Available online at:  
👉 **[https://noodles-simulator.onrender.com](https://noodles-simulator.onrender.com)**

> No installation needed. Just open the site and start solving 👇

---

## ✨ Highlights

- 🧠 Smart login with per-user statistics (Supabase-backed)
- 📊 Personal progress tracking (correct / total / success rate)
- 🏆 Global leaderboard
- 🎲 Strong randomness: shuffle-bag, least-seen bias, hourly throttle, session repeat-avoidance
- 🔐 Secure session handling and cookies
- 🔁 Reset progress feature for fresh runs
- ☁️ Fully deployed via Render

---

## 💾 Behind the Scenes

- Built with **C# + ASP.NET Razor Pages** (net8.0)
- Data layer: **Supabase** (PostgREST + Storage)
  - Images are stored in a Supabase Storage bucket
  - The app lists all images with pagination, groups every 5 files into a question set:  
    [0]=question, [1]=correct, [2..4]=wrongs (sorted by name)
- Randomization strategy:
  - Per-session shuffle-bag for coverage
  - Least-seen bias across lifetime of the instance
  - No more than 3 shows per question per hour
  - Avoid last 10 shown in current session
- Performance:
  - Signed URL and list caching
  - HTTP timeouts and reduced payload queries

---

## 🔧 Configuration

Set these environment variables (Render dashboard → Environment):

- SUPABASE_URL
- SUPABASE_ANON_KEY or SUPABASE_KEY
- SUPABASE_SERVICE_ROLE_KEY or SERVICE_ROLE_SECRET
- SUPABASE_BUCKET (e.g. noodles-images)
- SUPABASE_SIGNED_URL_TTL (e.g. 3600)
- EMAIL_TO (optional), EMAIL_SMTP_USER, EMAIL_SMTP_PASS, EMAIL_SMTP_SERVER (optional)

File naming guidance for best results:
- Keep total image count a multiple of 5
- Name files so sorting keeps each 5-file group together (e.g., 0001_..., 0002_...)

---

## 🔍 Debug

A lightweight diagnostics endpoint is available:
- **GET `/debug-random`** → shows throttled counts, usage histogram, and your session’s recent questions

Health check:
- **GET `/health`**

---

## 🚀 Deploy

- Render automatically builds via `dotnet publish -c Release`
- Manual deploy: Render → Service → Manual Deploy → Latest Commit

---

## 🙌 Contributing

Feel free to fork and build your own version.  
Pull requests, feedback, and stars ⭐ are always welcome!

---

## 📬 Contact

Created by **[Yaniv Bahalul](https://github.com/yanivbahalul)**  
For issues or ideas – open a discussion on GitHub.
