import requests
import time

INTERVAL = 60  # 1 דקה (שניות)
CHECK_URL = "http://localhost:3000" # Replace with your actual check URL
RENDER_API_KEY = "rnd_aPNjt9rvXje6qUGTjI6atz8bl9Wo"
SERVICE_ID = "srv-d0957jqdbo4c73960bn0"

def restart_render_service():
    print("Restarting render service...")
    try:
        # Assuming a command to restart the service, e.g.,
        # os.system("systemctl restart render-service")
        # os.system("docker restart render-container")
        print("Restart command executed (placeholder).")
    except Exception as e:
        print(f"Error during restart: {e}")

def main():
    run_count = 0
    while True:
        run_count += 1
        print(f"\n--- Run number: {run_count} ---")
        try:
            resp = requests.get(CHECK_URL, timeout=10)
            if resp.status_code >= 500:
                print(f"Detected error {resp.status_code}, deploying last commit...")
                restart_render_service()
            else:
                print(f"Site is up: {resp.status_code}")
        except Exception as ex:
            print(f"Site unreachable: {ex}, deploying last commit...")
            restart_render_service()
        time.sleep(INTERVAL)

if __name__ == "__main__":
    main() 