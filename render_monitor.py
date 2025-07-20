print("=== Starting render_monitor.py === (pre-import)")
try:
    import requests
    import time
    import smtplib
    from email.mime.text import MIMEText
    import os
except Exception as e:
    print(f"IMPORT ERROR: {e}")
    raise

# === CONFIGURATION ===
RENDER_API_KEY = os.environ.get("RENDER_API_KEY")
SERVICE_ID = os.environ.get("SERVICE_ID")
CHECK_URL = "https://noodles-simulator.onrender.com"  # Hardcoded
CHECK_INTERVAL = 60  # seconds, hardcoded

# Email notification settings (non-secret values hardcoded)
EMAIL_SMTP_USER = "yanivbahlul@gmail.com"
EMAIL_SMTP_SERVER = "smtp.gmail.com"
EMAIL_FROM = "yanivbahlul@gmail.com"
EMAIL_TO = "yanivbahlul@gmail.com"
EMAIL_SUBJECT = "[Noodles Simulator] Restarted!"
EMAIL_SMTP_PASS = os.environ.get("EMAIL_SMTP_PASS")  # Secret only from env

# === END CONFIGURATION ===

def send_email_notification(reason):
    body = f"The server at {CHECK_URL} was restarted due to: {reason}"
    msg = MIMEText(body)
    msg["Subject"] = EMAIL_SUBJECT
    msg["From"] = EMAIL_FROM
    msg["To"] = EMAIL_TO
    try:
        with smtplib.SMTP(EMAIL_SMTP_SERVER, 587) as server:
            server.starttls()
            server.login(EMAIL_SMTP_USER, EMAIL_SMTP_PASS)
            server.sendmail(EMAIL_FROM, [EMAIL_TO], msg.as_string())
        print("Email notification sent.")
    except Exception as e:
        print(f"Failed to send email: {e}")

def restart_render_service():
    url = f"https://api.render.com/v1/services/{SERVICE_ID}/deploys"
    headers = {
        "Authorization": f"Bearer {RENDER_API_KEY}",
        "Content-Type": "application/json"
    }
    response = requests.post(url, headers=headers, json={})
    if response.status_code == 201:
        print("Restarted Render service successfully!")
        return True
    else:
        print(f"Failed to restart service: {response.status_code} {response.text}")
        return False

print("=== Starting render_monitor.py ===")

while True:
    try:
        resp = requests.get(CHECK_URL, timeout=10)
        if resp.status_code >= 500:
            print(f"Detected error {resp.status_code}, restarting service...")
            if restart_render_service():
                send_email_notification(f"HTTP {resp.status_code}")
        else:
            print(f"Site is up: {resp.status_code}")
    except Exception as e:
        print(f"Site unreachable: {e}, restarting service...")
        if restart_render_service():
            send_email_notification(f"Exception: {e}")
    time.sleep(CHECK_INTERVAL) 