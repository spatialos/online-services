import datetime
import json
import time

def formatEvent(index, event, batch_id, analytics_environment):
    event_new = event
    try:
        event_new['receivedTimestamp'] = time.time()
        event_new['batchId'] = batch_id
        event_new['eventId'] = batch_id + '/' + str(index)
        event_new['analyticsEnvironment'] = analytics_environment
        try:
            event_new['eventAttributes'] = json.dumps(event['eventAttributes'])
        except:
            pass
        return event_new
    except:
        return event

def dateTime():
    ts = datetime.datetime.utcnow()
    ts_fmt = ts.strftime('%Y-%m-%dT%H:%M:%S') + 'Z'
    ds = datetime.datetime.strftime(ts, '%Y-%m-%d')
    event_time = {0: '0-8', 1: '8-16', 2: '16-24'}[ts.hour // 8]
    return ts, ts_fmt, ds, event_time
