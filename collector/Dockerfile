FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    # Install Playwright browsers to a fixed path that any user can read.
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

WORKDIR /app

COPY pyproject.toml ./
COPY collector ./collector

# Install Python deps including Playwright, then install Chromium system
# libraries and the Chromium binary — all as root so apt-get can run.
# chmod ensures the non-root user can read the browser files at runtime.
RUN pip install --upgrade pip \
    && pip install ".[playwright]" \
    && playwright install-deps chromium \
    && playwright install chromium \
    && chmod -R a+rX /ms-playwright

# Create non-root user after browser installation.
RUN addgroup --system collector \
    && adduser --system --ingroup collector collector

USER collector

CMD ["python", "-m", "collector.main", "--skip-lookback"]
