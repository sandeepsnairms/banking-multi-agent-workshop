set -e  # Exit on error

# Log the environment variable
echo "Using apiUrl:: $apiUrl:"

# Overwrite environment.ts
echo "export const environment = { apiUrl: '$apiUrl' };" > /app/src/environments/environment.ts


# Start Angular
exec ng serve --host 0.0.0.0 --port 80 --disable-host-check
