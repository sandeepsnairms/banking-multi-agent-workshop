import logging
import uuid
from datetime import datetime
from typing import Dict, List

from langchain_core.runnables import RunnableConfig
from langchain_core.tools import tool
from langsmith import traceable

from src.app.services.azure_cosmos_db import create_service_request_record


@tool
@traceable
def service_request(config: RunnableConfig,  recipientPhone: str, recipientEmail: str,
                    requestSummary: str) -> str:
    """
    Create a service request entry in the AccountsData container.

    :param config: Configuration dictionary.
    :param tenantId: The ID of the tenant.
    :param userId: The ID of the user.
    :param recipientPhone: The phone number of the recipient.
    :param recipientEmail: The email address of the recipient.
    :param requestSummary: A summary of the service request.
    :return: A message indicating the result of the operation.
    """
    try:
        tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
        userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
        request_id = str(uuid.uuid4())
        requested_on = datetime.utcnow().isoformat() + "Z"
        request_annotations = [
            requestSummary,
            f"[{datetime.utcnow().strftime('%d-%m-%Y %H:%M:%S')}] : Urgent"
        ]

        service_request_data = {
            "id": request_id,
            "tenantId": tenantId,
            "userId": userId,
            "type": "ServiceRequest",
            "requestedOn": requested_on,
            "scheduledDateTime": "0001-01-01T00:00:00",
            "accountId": "A1",
            "srType": 0,
            "recipientEmail": recipientEmail,
            "recipientPhone": recipientPhone,
            "debitAmount": 0,
            "isComplete": False,
            "requestAnnotations": request_annotations,
            "fulfilmentDetails": None
        }

        create_service_request_record(service_request_data)
        return f"Service request created successfully with ID: {request_id}"
    except Exception as e:
        logging.error(f"Error creating service request: {e}")
        return f"Failed to create service request: {e}"


@tool
@traceable
def get_branch_location(state: str) -> Dict[str, List[str]]:
    """
    Get location of bank branches for a given state in the USA.

    :param state: The name of the state.
    :return: A dictionary with county names as keys and lists of branch names as values.
    """
    branches = {
        "Alabama": {"Jefferson County": ["Central Bank - Birmingham", "Trust Bank - Hoover"],
                    "Mobile County": ["Central Bank - Mobile", "Trust Bank - Prichard"]},
        "Alaska": {"Anchorage": ["Central Bank - Anchorage", "Trust Bank - Eagle River"],
                   "Fairbanks North Star Borough": ["Central Bank - Fairbanks", "Trust Bank - North Pole"]},
        "Arizona": {"Maricopa County": ["Central Bank - Phoenix", "Trust Bank - Scottsdale"],
                    "Pima County": ["Central Bank - Tucson", "Trust Bank - Oro Valley"]},
        "Arkansas": {"Pulaski County": ["Central Bank - Little Rock", "Trust Bank - North Little Rock"],
                     "Benton County": ["Central Bank - Bentonville", "Trust Bank - Rogers"]},
        "California": {"Los Angeles County": ["Central Bank - Los Angeles", "Trust Bank - Long Beach"],
                       "San Diego County": ["Central Bank - San Diego", "Trust Bank - Chula Vista"]},
        "Colorado": {"Denver County": ["Central Bank - Denver", "Trust Bank - Aurora"],
                     "El Paso County": ["Central Bank - Colorado Springs", "Trust Bank - Fountain"]},
        "Connecticut": {"Fairfield County": ["Central Bank - Bridgeport", "Trust Bank - Stamford"],
                        "Hartford County": ["Central Bank - Hartford", "Trust Bank - New Britain"]},
        "Delaware": {"New Castle County": ["Central Bank - Wilmington", "Trust Bank - Newark"],
                     "Sussex County": ["Central Bank - Seaford", "Trust Bank - Lewes"]},
        "Florida": {"Miami-Dade County": ["Central Bank - Miami", "Trust Bank - Hialeah"],
                    "Orange County": ["Central Bank - Orlando", "Trust Bank - Winter Park"]},
        "Georgia": {"Fulton County": ["Central Bank - Atlanta", "Trust Bank - Sandy Springs"],
                    "Cobb County": ["Central Bank - Marietta", "Trust Bank - Smyrna"]},
        "Hawaii": {"Honolulu County": ["Central Bank - Honolulu", "Trust Bank - Pearl City"],
                   "Maui County": ["Central Bank - Kahului", "Trust Bank - Lahaina"]},
        "Idaho": {"Ada County": ["Central Bank - Boise", "Trust Bank - Meridian"],
                  "Canyon County": ["Central Bank - Nampa", "Trust Bank - Caldwell"]},
        "Illinois": {"Cook County": ["Central Bank - Chicago", "Trust Bank - Evanston"],
                     "DuPage County": ["Central Bank - Naperville", "Trust Bank - Wheaton"]},
        "Indiana": {"Marion County": ["Central Bank - Indianapolis", "Trust Bank - Lawrence"],
                    "Lake County": ["Central Bank - Gary", "Trust Bank - Hammond"]},
        "Iowa": {"Polk County": ["Central Bank - Des Moines", "Trust Bank - West Des Moines"],
                 "Linn County": ["Central Bank - Cedar Rapids", "Trust Bank - Marion"]},
        "Kansas": {"Sedgwick County": ["Central Bank - Wichita", "Trust Bank - Derby"],
                   "Johnson County": ["Central Bank - Overland Park", "Trust Bank - Olathe"]},
        "Kentucky": {"Jefferson County": ["Central Bank - Louisville", "Trust Bank - Jeffersontown"],
                     "Fayette County": ["Central Bank - Lexington", "Trust Bank - Nicholasville"]},
        "Louisiana": {"Orleans Parish": ["Central Bank - New Orleans", "Trust Bank - Metairie"],
                      "East Baton Rouge Parish": ["Central Bank - Baton Rouge", "Trust Bank - Zachary"]},
        "Maine": {"Cumberland County": ["Central Bank - Portland", "Trust Bank - South Portland"],
                  "Penobscot County": ["Central Bank - Bangor", "Trust Bank - Brewer"]},
        "Maryland": {"Baltimore County": ["Central Bank - Baltimore", "Trust Bank - Towson"],
                     "Montgomery County": ["Central Bank - Rockville", "Trust Bank - Bethesda"]},
        "Massachusetts": {"Suffolk County": ["Central Bank - Boston", "Trust Bank - Revere"],
                          "Worcester County": ["Central Bank - Worcester", "Trust Bank - Leominster"]},
        "Michigan": {"Wayne County": ["Central Bank - Detroit", "Trust Bank - Dearborn"],
                     "Oakland County": ["Central Bank - Troy", "Trust Bank - Farmington Hills"]},
        "Minnesota": {"Hennepin County": ["Central Bank - Minneapolis", "Trust Bank - Bloomington"],
                      "Ramsey County": ["Central Bank - Saint Paul", "Trust Bank - Maplewood"]},
        "Mississippi": {"Hinds County": ["Central Bank - Jackson", "Trust Bank - Clinton"],
                        "Harrison County": ["Central Bank - Gulfport", "Trust Bank - Biloxi"]},
        "Missouri": {"Jackson County": ["Central Bank - Kansas City", "Trust Bank - Independence"],
                     "St. Louis County": ["Central Bank - St. Louis", "Trust Bank - Florissant"]},
        "Montana": {"Yellowstone County": ["Central Bank - Billings", "Trust Bank - Laurel"],
                    "Missoula County": ["Central Bank - Missoula", "Trust Bank - Lolo"]},
        "Nebraska": {"Douglas County": ["Central Bank - Omaha", "Trust Bank - Bellevue"],
                     "Lancaster County": ["Central Bank - Lincoln", "Trust Bank - Waverly"]},
        "Nevada": {"Clark County": ["Central Bank - Las Vegas", "Trust Bank - Henderson"],
                   "Washoe County": ["Central Bank - Reno", "Trust Bank - Sparks"]},
        "New Hampshire": {"Hillsborough County": ["Central Bank - Manchester", "Trust Bank - Nashua"],
                          "Rockingham County": ["Central Bank - Portsmouth", "Trust Bank - Derry"]},
        "New Jersey": {"Essex County": ["Central Bank - Newark", "Trust Bank - East Orange"],
                       "Bergen County": ["Central Bank - Hackensack", "Trust Bank - Teaneck"]},
        "New Mexico": {"Bernalillo County": ["Central Bank - Albuquerque", "Trust Bank - Rio Rancho"],
                       "Santa Fe County": ["Central Bank - Santa Fe", "Trust Bank - Eldorado"]},
        "New York": {"New York County": ["Central Bank - Manhattan", "Trust Bank - Harlem"],
                     "Kings County": ["Central Bank - Brooklyn", "Trust Bank - Williamsburg"]},
        "North Carolina": {"Mecklenburg County": ["Central Bank - Charlotte", "Trust Bank - Matthews"],
                           "Wake County": ["Central Bank - Raleigh", "Trust Bank - Cary"]},
        "North Dakota": {"Cass County": ["Central Bank - Fargo", "Trust Bank - West Fargo"],
                         "Burleigh County": ["Central Bank - Bismarck", "Trust Bank - Lincoln"]},
        "Ohio": {"Cuyahoga County": ["Central Bank - Cleveland", "Trust Bank - Parma"],
                 "Franklin County": ["Central Bank - Columbus", "Trust Bank - Dublin"]},
        "Oklahoma": {"Oklahoma County": ["Central Bank - Oklahoma City", "Trust Bank - Edmond"],
                     "Tulsa County": ["Central Bank - Tulsa", "Trust Bank - Broken Arrow"]},
        "Oregon": {"Multnomah County": ["Central Bank - Portland", "Trust Bank - Gresham"],
                   "Lane County": ["Central Bank - Eugene", "Trust Bank - Springfield"]},
        "Pennsylvania": {"Philadelphia County": ["Central Bank - Philadelphia", "Trust Bank - Germantown"],
                         "Allegheny County": ["Central Bank - Pittsburgh", "Trust Bank - Bethel Park"]},
        "Rhode Island": {"Providence County": ["Central Bank - Providence", "Trust Bank - Cranston"],
                         "Kent County": ["Central Bank - Warwick", "Trust Bank - Coventry"]},
        "South Carolina": {"Charleston County": ["Central Bank - Charleston", "Trust Bank - Mount Pleasant"],
                           "Richland County": ["Central Bank - Columbia", "Trust Bank - Forest Acres"]},
        "South Dakota": {"Minnehaha County": ["Central Bank - Sioux Falls", "Trust Bank - Brandon"],
                         "Pennington County": ["Central Bank - Rapid City", "Trust Bank - Box Elder"]},
        "Tennessee": {"Davidson County": ["Central Bank - Nashville", "Trust Bank - Antioch"],
                      "Shelby County": ["Central Bank - Memphis", "Trust Bank - Bartlett"]},
        "Texas": {"Harris County": ["Central Bank - Houston", "Trust Bank - Pasadena"],
                  "Dallas County": ["Central Bank - Dallas", "Trust Bank - Garland"]},
        "Utah": {"Salt Lake County": ["Central Bank - Salt Lake City", "Trust Bank - West Valley City"],
                 "Utah County": ["Central Bank - Provo", "Trust Bank - Orem"]},
        "Vermont": {"Chittenden County": ["Central Bank - Burlington", "Trust Bank - South Burlington"],
                    "Rutland County": ["Central Bank - Rutland", "Trust Bank - Killington"]},
        "Virginia": {"Fairfax County": ["Central Bank - Fairfax", "Trust Bank - Reston"],
                     "Virginia Beach": ["Central Bank - Virginia Beach", "Trust Bank - Chesapeake"]},
        "Washington": {"King County": ["Central Bank - Seattle", "Trust Bank - Bellevue"],
                       "Pierce County": ["Central Bank - Tacoma", "Trust Bank - Lakewood"]},
        "West Virginia": {"Kanawha County": ["Central Bank - Charleston", "Trust Bank - South Charleston"],
                          "Berkeley County": ["Central Bank - Martinsburg", "Trust Bank - Hedgesville"]},
        "Wisconsin": {"Milwaukee County": ["Central Bank - Milwaukee", "Trust Bank - Wauwatosa"],
                      "Dane County": ["Central Bank - Madison", "Trust Bank - Fitchburg"]},
        "Wyoming": {"Laramie County": ["Central Bank - Cheyenne", "Trust Bank - Ranchettes"],
                    "Natrona County": ["Central Bank - Casper", "Trust Bank - Mills"]}
    }

    return branches.get(state, {"Unknown County": ["No branches available", "No branches available"]})
