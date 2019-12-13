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


def try_format_playfab_event(event, batch_id, analytics_environment, playfab_root_fields):

    """ Whenever URL paramter `&event_category=` is set to `playfab` when POST'ing events
    to our Cloud Endpoint, this event formatting function is used instead, which better
    handles PlayFab's event JSON schema & ensures the data is accessible with BigQuery later on.

    This can be useful when you have deployed PlayFab services & would like to capture their
    out-of-the-box analytics events, and place them alongside your other events. In order to
    enable this, configure PlayFab's webhook forwarding method to pipe events towards the Cloud Endpoint.

    Tip - You must set the `event_category` URL parameter to `playfab` for this to work properly!

    Also see: https://api.playfab.com/docs/tutorials/landing-analytics/webhooks
    """

    try:
        event_new = {}
        event_attributes = {}
        for attribute in event.keys():
            if attribute in playfab_root_fields:
                if isinstance(event[attribute], dict):
                    event_new[attribute] = json.dumps(event[attribute])
                else:
                    event_new[attribute] = event[attribute]
            else:
                event_attributes[attribute] = event[attribute]
        event_new['BatchId'] = batch_id
        event_new['ReceivedTimestamp'] = time.time()
        event_new['AnalyticsEnvironment'] = analytics_environment
        event_new['EventAttributes'] = json.dumps(event_attributes)
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
