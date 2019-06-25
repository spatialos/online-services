import datetime
import json

def pathParser(path, key):
    try:
        value = path.split(key)[1].split('/')[0]
        if key == 'event_ds=':
            try:
                x = datetime.datetime.strptime(value, '%Y-%m-%d')
            except:
                value = None
    except:
        value = None
    return value

def envParser(environment):
    if environment in ['all', '']:
        list_env, name_env = ['testing', 'development', 'staging', 'production', 'live'], 'all-envs'
    else:
        list_env, name_env = [environment], environment
    return list_env, name_env

def timeParser(time_part):
    if time_part == 'all':
        list_time_part, name_time = ['0-8', '8-16', '16-24'], time_part + '-times'
    else:
        list_time_part, name_time = [time_part], time_part
    return list_time_part, name_time

def jsonParser(text):
    try: # List or Dict JSON
        x = json.loads(text)
    except:
        try: # Newline Delimited JSON
            x = []
            for i in text.split('\n'):
                x.append(json.loads(i))
        except Exception as e:
            x = 'Error: {e} -- The following string could not be parsed as JSON: {text}'.format(e = e, text = text)
    return x

def parseField(_dict, option1, option2 = ['']):
    if len(option1) == 1 and len(option2) == 1:
        try:
            v = _dict[option1[0]]
        except:
            try:
                v = _dict[option2[0]]
            except:
                v = None
        return v

def unixTimestampCheck(ts):
    from datetime import datetime
    import time

    if isinstance(ts, float) or isinstance(ts, int):
        return ts

    ts_list = []
    if isinstance(ts, str):
        for i in ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z']:
            try:
                d = datetime.strptime(ts, i)
                ts_list.append(time.mktime(d.timetuple()))
            except:
                pass
    ts_list.append(None)
    return ts_list[0]
