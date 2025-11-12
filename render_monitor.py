#!/usr/bin/env python3

import logging
import signal
import sys
import time


def _handle_signal(signum, frame):  # pragma: no cover
    logging.info("render_monitor.py received signal %s, exiting.", signum)
    sys.exit(0)


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )
    logging.info("render_monitor.py no-op monitor started.")
    signal.signal(signal.SIGTERM, _handle_signal)
    signal.signal(signal.SIGINT, _handle_signal)

    try:
        while True:
            time.sleep(60)
    except SystemExit:
        raise
    except Exception:  # pragma: no cover
        logging.exception("render_monitor.py encountered an unexpected error.")
        sys.exit(1)


if __name__ == "__main__":
    main()

