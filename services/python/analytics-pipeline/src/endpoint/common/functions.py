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


def try_format_playfab_event(event, batch_id, analytics_environment):

    """ Whenever URL paramter `&event_category=` is set to `playfab` when POST'ing events
    to our Cloud Endpoint, this event formatting function is used instead, which better
    handles PlayFab's event JSON schema & ensures the data is accessible with BigQuery later on.

    This can be useful when you have deployed PlayFab services & would like to capture their
    out-of-the-box analytics events, and place them alongside your other events. In order to
    enable this, configure PlayFab's webhook forwarding method to pipe events towards the Cloud Endpoint.

    Tip - You must set the `event_category` URL parameter to `playfab` for this to work properly!

    Also see: https://api.playfab.com/docs/tutorials/landing-analytics/webhooks
    """

    playfab_keys = ['TitleId', 'Timestamp', 'SourceType', 'Source', 'PlayFabEnvironment',
                    'EventNamespace', 'EventName', 'EventId', 'EntityType', 'EntityId']

    try:
        new_event, new_event_attributes = dict(), dict()
        for key in event.keys():
            if key in playfab_keys:
                if isinstance(event[key], dict):
                    new_event[key] = json.dumps(event[key])
                else:
                    new_event[key] = event[key]
            else:
                new_event_attributes[key] = event[key]
        new_event['BatchId'] = batch_id
        new_event['ReceivedTimestamp'] = time.time()
        new_event['AnalyticsEnvironment'] = analytics_environment
        new_event['EventAttributes'] = json.dumps(new_event_attributes)
        return [True, new_event]
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
