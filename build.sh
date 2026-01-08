#!/usr/bin/env bash
# exit on error
set -o errexit

pip install -r vr_backend/requirements.txt

cd vr_backend
python manage.py collectstatic --no-input
python manage.py migrate
