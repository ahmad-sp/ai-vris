#!/usr/bin/env bash
# exit on error
set -o errexit

# Navigate to vr_backend directory
cd vr_backend

# Install dependencies
pip install -r requirements.txt

# Collect static files
python manage.py collectstatic --no-input

# Run migrations
python manage.py migrate

