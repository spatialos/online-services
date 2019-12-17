# Python 3.7.1

# python scale_test.py \
#   --gcp-secret-path={{path_to_local_sa_json_key_file}} \
#   --host=http://analytics-testing.endpoints.{{your_google_project_id}}.cloud.goog:80/ \
#   --api-key={{your_analytics_api_key}} \
#   --pool-size=30 \
#   --n=10
#   --bucket-name={{your_google_project_id}}-analytics-testing \
#   --event-schema=improbable \
#   --event-category=native \
#   --event-environment=debug \
#   --scale-test-name=scale-test \

from multiprocessing.pool import ThreadPool as Pool
from google.cloud import storage
from datetime import datetime
from six.moves import urllib
import argparse
import requests
import time

parser = argparse.ArgumentParser()
# Parameters around general execution:
parser.add_argument('--gcp-secret-path', dest='gcp_secret_path', required=True)
parser.add_argument('--pool-size', dest='pool_size', required=True)
parser.add_argument('--api-key', dest='api_key', required=True)
parser.add_argument('--host', required=True)
parser.add_argument('--verbose', default=1)
parser.add_argument('--n', required=True)
# Parameters to specify how the files end up in Google Cloud Storage:
parser.add_argument('--bucket-name', dest='bucket_name', required=True)
parser.add_argument('--event-schema', dest='event_schema', required=True)
parser.add_argument('--event-category', dest='event_category', required=True)
parser.add_argument('--event-environment', dest='event_environment', required=True)
parser.add_argument('--event-ds', dest='event_ds', default='compute')
parser.add_argument('--event-time', dest='event_time', default='compute')
parser.add_argument('--scale-test-name', dest='scale_test_name', required=True)

args = parser.parse_args()


def verbose(input):
    if int(args.verbose) == 1:
        print(input)


def make_request(i, message, scale_test_name):
    import json
    import time
    url = urllib.parse.urljoin(args.host, 'v1/event')
    params = {
        'key': args.api_key,
        'event_schema': args.event_schema,
        'event_category': args.event_category,
        'event_environment': args.event_environment,
        'event_ds': event_ds,
        'event_time': event_time,
        'session_id': scale_test_name + '/f58179a375290599dde17f7c6d546d78',
    }

    for index, event in enumerate(message):
        message[index]['eventIndex'] = i
        message[index]['eventTimestamp'] = time.time()
        message[index]['eventType'] = scale_test_name

    headers = {'content-type': 'application/json'}
    response = requests.post(url, params=params, json=message, headers=headers)
    response.raise_for_status()
    verbose(response.text)
    return


client_storage = storage.Client.from_service_account_json(args.gcp_secret_path)
bucket = client_storage.get_bucket(args.bucket_name)

ts = datetime.utcnow()
if args.ds == 'compute':
    event_ds = ts.strftime('%Y-%m-%d')
else:
    event_ds = args.ds

if args.time_part == 'compute':
    m = {0: '0-8', 1: '8-16', 2: '16-24'}
    event_time = m[ts.hour // 8]
else:
    event_time = args.time_part

message = [{"eventSource":"client","eventClass":"buildkite","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"debug","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}, {"eventSource":"client","eventClass":"session","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"debug","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}, {"eventSource":"client","eventClass":"game","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"debug","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}}, "playerId":"12345678"},{"eventSource":"client","eventClass":"inventory","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"debug","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}]

scale_test_name = f'{args.scale_test_name}-{args.n}-{int(time.time())}'


def run():

    pool = Pool(int(args.pool_size))

    n = 0
    start = datetime.now()
    for i in range(0, int(args.n)):
        pool.apply_async(make_request, (i, message, scale_test_name))
        n += 1

    pool.close()
    pool.join()

    end = datetime.now()

    prefix = f'data_type=jsonl/event_schema={args.event_schema}/event_category={args.event_category}/event_environment={args.event_environment}/event_ds={event_ds}/event_time={event_time}/{scale_test_name}'
    blobs = list(bucket.list_blobs(prefix=prefix))

    verbose(f'Number of threads used: {args.pool_size}')
    verbose(f'Number of files sent to Endpoint: {n}')
    verbose(f'Number of files stored in GCS: {len(blobs)}')

    accuracy = 1.0 * len(blobs) / int(args.n)

    if accuracy >= 0.99:
        verbose(f'Scale test succeeded! Accuracy: {accuracy:.2%}')
    else:
        verbose(f'Scale test failed! Accuracy: {accuracy:.2%}')

    verbose(f'Run took {str(end - start)}')
    verbose('\n')

    batch_frequencies = [10, 20, 30]
    for i in batch_frequencies:
        supported_ccu = (1.0 * int(args.n)) / ((end - start).total_seconds() / i)
        verbose(f'At {i} seconds per player per file, this means we can at least support {int(supported_ccu)} CCU\'s')
    verbose('\n')

    verbose(f'Files written to: {prefix}')
    verbose('\n')

    return f'Scale test name: {scale_test_name} | Accuracy: {accuracy}'


print(run())

# Example result:

# Number of threads used: 30
# Number of files sent to Endpoint: 10000
# Number of files stored in GCS: 10000
# Scale test succeeded! Accuracy: 100.00%
# Run took 0:00:59.371451

# At 10 seconds per player per file, this means we can at least support 1684 CCU's
# At 20 seconds per player per file, this means we can at least support 3368 CCU's
# At 30 seconds per player per file, this means we can at least support 5052 CCU's
