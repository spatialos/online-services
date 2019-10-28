# Python 3.6.5

# python scale_test.py \
#   --gcp-secret-path=[local JSON key path] \
#   --host=http://analytics.endpoints.[your project id].cloud.goog:80/ \
#   --api-key=[your gcp api key] \
#   --bucket-name=[your project id]-analytics \
#   --scale-test-name=scale-test \
#   --event-category=scale-test \
#   --analytics-environment=testing \
#   --pool-size=30 \
#   --n=10

from multiprocessing.pool import ThreadPool as Pool
from google.cloud import storage
from datetime import datetime
from six.moves import urllib
import argparse
import requests
import time

parser = argparse.ArgumentParser()
parser.add_argument('--analytics-environment', dest='analytics_environment', required=True)
parser.add_argument('--gcp-secret-path', dest='gcp_secret_path', required=True)
parser.add_argument('--scale-test-name', dest='scale_test_name', required=True)
parser.add_argument('--event-category', dest='event_category', required=True)
parser.add_argument('--bucket-name', dest='bucket_name', required=True)
parser.add_argument('--time-part', dest='time_part', default='compute')
parser.add_argument('--pool-size', dest='pool_size', required=True)
parser.add_argument('--api-key', dest='api_key', required=True)
parser.add_argument('--ds', default='compute')
parser.add_argument('--host', required=True)
parser.add_argument('--verbose', default=1)
parser.add_argument('--n', required=True)
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
        'analytics_environment': args.analytics_environment,
        'event_category': args.event_category,
        'session_id': scale_test_name + '/f58179a375290599dde17f7c6d546d78',
        'ds': ds,
        'time': event_time
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
    ds = ts.strftime('%Y-%m-%d')
else:
    ds = args.ds

if args.time_part == 'compute':
    m = {0: '0-8', 1: '8-16', 2: '16-24'}
    event_time = m[ts.hour // 8]
else:
    event_time = args.time_part

message = [{"eventSource":"client","eventClass":"buildkite","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"testing","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}, {"eventSource":"client","eventClass":"session","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"testing","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}, {"eventSource":"client","eventClass":"game","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"testing","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}}, "playerId":"12345678"},{"eventSource":"client","eventClass":"inventory","eventType":"session_start","eventTimestamp":time.time(),"eventIndex":6,"sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"2.0.13","eventEnvironment":"testing","eventAttributes":{"eventData":{"controllers":[{"model":"Intel HD Graphics 630","bus":"Built-In","vram":1536,"vramDynamic":True,"vendor":"Intel"},{"model":"Radeon Pro 560","bus":"PCIe","vram":4096,"vramDynamic":True,"vendor":"AMD"}],"displays":[{"model":"Color LCD","main":False,"builtin":False,"connection":"","sizex":-1,"sizey":-1,"resolutionx":2880,"resolutiony":1800},{"model":"DELL U3417W","main":True,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":3440,"resolutiony":1440},{"model":"DELL U2414H","main":False,"builtin":False,"connection":"DisplayPort","sizex":-1,"sizey":-1,"resolutionx":1080,"resolutiony":1920}]}},"playerId":"12345678"}]

scale_test_name = '{scale_test_name}-{n}-{time}'.format(
  scale_test_name=args.scale_test_name, n=str(args.n), time=str(int(time.time())))


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

    prefix = 'data_type=json/analytics_environment={analytics_environment}/event_category={event_category}/event_ds={ds}/event_time={event_time}/{scale_test_name}'.format(
      analytics_environment=args.analytics_environment, event_category=args.event_category, ds=ds, event_time=event_time, scale_test_name=scale_test_name)
    blobs = list(bucket.list_blobs(prefix=prefix))

    verbose('Number of threads used: {n}'.format(n=args.pool_size))
    verbose('Number of files sent to Endpoint: {n}'.format(n=str(n)))
    verbose('Number of files stored in GCS: {n}'.format(n=len(blobs)))

    accuracy = 1.0 * len(blobs) / int(args.n)

    if accuracy >= 0.99:
        verbose('Scale test succeeded! Accuracy: {:.2%}'.format(accuracy))
    else:
        verbose('Scale test failed! Accuracy: {:.2%}'.format(accuracy))

    verbose('Run took {length}'.format(length=str(end - start)))
    verbose('\n')

    batch_frequencies = [10, 20, 30]
    for i in batch_frequencies:
        supported_ccu = (1.0 * int(args.n)) / ((end - start).total_seconds() / i)
        verbose('At {i} seconds per player per file, this means we can at least support {x} CCU\'s'.format(i=i, x=str(int(supported_ccu))))
    verbose('\n')

    verbose('Files written to: {prefix}'.format(prefix=prefix))
    verbose('\n')

    verbose('Tip - Run the following to remove all the events you just created:')

    cleanup = """
    python src/cleanup-gcs.py \\
      --gcp-secret-path={gcp_secret_path} \\
      --scale-test-name={scale_test_name} \\
      --bucket-name={bucket_name} \\
      --event-category={event_category} \\
      --analytics-environment={analytics_environment} \\
      --pool-size={pool_size} \\
      --time-part={event_time} \\
      --ds={ds}
    """.format(gcp_secret_path=args.gcp_secret_path, scale_test_name=scale_test_name, bucket_name=args.bucket_name,
               event_category=args.event_category, analytics_environment=args.analytics_environment, pool_size=args.pool_size,
               event_time=event_time, ds=ds)

    verbose(cleanup)

    if int(args.verbose) == 0:
        if accuracy >= 0.99:
            return [1, scale_test_name]
        else:
            return [0, scale_test_name]
    else:
        return 'Scale test name: {scale_test_name}'.format(scale_test_name=scale_test_name)


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
