/** Option sets for the Student Profile → Needs tab, ported verbatim from the legacy `StudentProfile.js`
 *  constants (`allInterests`, `dental_student_specialty`, `allSepcialtyLocations`, `importants`).
 *  Selections are stored as these title strings (not the legacy positional ids). */

/** Interests grid for the medical (non-dental) tracks — exact legacy `allInterests` order. */
export const INTERESTS_MEDICAL = [
  "Internal Medicine", "General Surgery", "Family Medicine", "Psychiatry", "Neurology", "Pathology",
  "Pediatrics", "OBGYN", "Orthopedic Surgery", "Radiology", "Anesthesiology", "Dermatology",
  "Emergency Medicine", "Hematology/Oncology", "All Core Rotations"
];

/** Interests grid for the dental track — exact legacy `dental_student_specialty` order. */
export const INTERESTS_DENTAL = [
  "General Dentist", "Pediatric Dentist", "Orthodontist", "Periodontist", "Endodontist",
  "Prosthodontist", "Oral Pathologist/Oral Surgeon"
];

/** Preferred-location options — legacy `allSepcialtyLocations` verbatim, with the one duplicate "Chicago"
 *  (legacy id 43) dropped so titles are unique (selections are keyed by title). "Other" reveals a
 *  free-text field. Legacy spellings preserved ("Las Vagas", "Winston–Salem"). */
export const SPECIALTY_LOCATIONS = [
  "New York", "Los Angeles", "Chicago", "Houston", "Philadelphia", "Phoenix", "San Antonio", "San Diego",
  "Dallas", "San Jose", "Austin", "Jacksonville", "Indianapolis", "San Francisco", "Columbus", "Fort Worth",
  "Charlotte", "Detroit", "El Paso", "Memphis", "Boston", "Denver", "Washington", "Nashville", "Baltimore",
  "Louisville", "Portland", "Oklahoma", "Milwaukee", "Las Vagas", "Albuquerque", "Tucson", "Fresno",
  "Sacramento", "Long Beach", "Kansas City", "Mesa", "Virginia Beach", "Atlanta", "Colorado Springs",
  "Raleigh", "Omaha", "Miami", "Oakland", "Tulsa", "Cleveland", "Arlington", "Bakersfield", "Tampa",
  "Honolulu", "Anaheim", "Aurora", "Santa Ana", "St. Louis", "Riverside", "Corpus Christi", "Pittsburgh",
  "Lexington", "Anchorage", "Stockton", "Cincinnati", "Saint Paul", "Toledo", "Newark", "Greensboro",
  "Plano", "Henderson", "Lincoln", "Buffalo", "Fort Wayne", "Jersey City", "Chula Vista", "Orlando",
  "St. Petersburg", "Norfolk", "Chandler", "Laredo", "Madison", "Durham", "Lubbock", "Winston–Salem",
  "Garland", "Glendale", "Hialeah", "Reno", "Baton Rouge", "Irvine", "Chesapeake", "Irving", "Scottsdale",
  "North Las Vegas", "Fremont", "Gilbert", "San Bernardino", "Boise", "Birmingham", "Other"
];

/** "What are most important to you when finding a clinical rotation?" — exact legacy `importants` order.
 *  Hidden for the dental track. */
export const IMPORTANTS = [
  "Academic affiliation", "University based", "Inpatient exposure", "Outpatient", "Hands-on", "Inpatient",
  "LOR on hospital letterhead", "Residency audition"
];
