import datetime
import json
import time


def try_format_event(index, event, batch_id, analytics_environment):

    """ This function tries to augment an event with several attributes, and casts
    eventAttributes as a string whenever it is a list or a dictionary. This enables
    it to be written into BigQuery as-is, and to be subsequently parsed with BigQuery JSON functions.

    It returns a list which contains as its first element a boolean indicating whether
    the operation succeeded, and either the formatted event if the first element is true,
    or the original event if false.
    """

    event_new = event
    try:
        event_new['receivedTimestamp'] = time.time()
        event_new['batchId'] = batch_id
        event_new['eventId'] = '{batch_id}/{index}'.format(batch_id=batch_id, index=str(index))
        event_new['analyticsEnvironment'] = analytics_environment
        try:
            if isinstance(event['eventAttributes'], (dict, list)):
                event_new['eventAttributes'] = json.dumps(event['eventAttributes'])
            else:
                event_new['eventAttributes'] = str(event['eventAttributes'])
        except KeyError:
            event_new['eventAttributes'] = '{}'
        return [True, event_new]
    except Exception:
        return [False, event]


def get_date_time():

    """ This function captures several datetime values at a single point in time:
    whenever the function is called.
    """

    ts = datetime.datetime.utcnow()
    ts_fmt = ts.strftime('%Y-%m-%dT%H:%M:%S') + 'Z'
    ds = datetime.datetime.strftime(ts, '%Y-%m-%d')
    event_time = {0: '0-8', 1: '8-16', 2: '16-24'}[ts.hour // 8]
    return ts_fmt, ds, event_time
