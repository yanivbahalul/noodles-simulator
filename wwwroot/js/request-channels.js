(function () {
    const backgroundQueue = [];
    let idleHandle = null;
    let quizBusy = false;

    function supportsFetchPriority() {
        try {
            const probe = new Request("/", { priority: "high" });
            return probe.priority === "high";
        } catch {
            return false;
        }
    }

    const hasFetchPriority = supportsFetchPriority();

    function withPriority(init, priority) {
        const base = init ?? {};
        if (!hasFetchPriority) return base;
        return { ...base, priority };
    }

    function quizFetch(url, init) {
        return fetch(url, withPriority(init, "high"));
    }

    function waitForQuizIdle() {
        if (!quizBusy) return Promise.resolve();
        return new Promise((resolve) => {
            const tick = () => {
                if (!quizBusy) resolve();
                else setTimeout(tick, 32);
            };
            tick();
        });
    }

    function pumpBackgroundQueue() {
        if (quizBusy || idleHandle || backgroundQueue.length === 0) return;

        const runNext = async () => {
            idleHandle = null;
            if (quizBusy || backgroundQueue.length === 0) {
                pumpBackgroundQueue();
                return;
            }

            const task = backgroundQueue.shift();
            try {
                await waitForQuizIdle();
                await task();
            } catch {
                // background tasks are best-effort
            } finally {
                pumpBackgroundQueue();
            }
        };

        if (typeof requestIdleCallback === "function") {
            idleHandle = requestIdleCallback(() => {
                runNext();
            }, { timeout: 4000 });
        } else {
            idleHandle = setTimeout(runNext, 150);
        }
    }

    function scheduleBackground(task) {
        backgroundQueue.push(task);
        pumpBackgroundQueue();
    }

    function backgroundFetch(url, init) {
        return new Promise((resolve, reject) => {
            scheduleBackground(async () => {
                try {
                    resolve(await fetch(url, withPriority(init, "low")));
                } catch (err) {
                    reject(err);
                }
            });
        });
    }

    function notifyQuizBusy(on) {
        quizBusy = Boolean(on);
        if (quizBusy && idleHandle) {
            if (typeof cancelIdleCallback === "function") {
                cancelIdleCallback(idleHandle);
            } else {
                clearTimeout(idleHandle);
            }
            idleHandle = null;
        } else if (!quizBusy) {
            pumpBackgroundQueue();
        }
    }

    window.RequestChannels = {
        quizFetch,
        backgroundFetch,
        scheduleBackground,
        notifyQuizBusy,
        hasFetchPriority,
        getBackgroundQueueLength() {
            return backgroundQueue.length;
        },
        isQuizBusy() {
            return quizBusy;
        }
    };
})();
