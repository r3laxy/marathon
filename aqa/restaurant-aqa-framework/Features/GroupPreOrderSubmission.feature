@api @group-preorder @level2
Feature: Group pre-order submission

  Guests confirm their own carts; when everybody is ready the owner submits the group
  pre-order. Only confirmed carts are taken over, merged into the owner's own pre-order
  of the reservation, and after that the group is closed for changes.

  Background:
    Given the restaurant has a waiter account
    And a restaurant with 2 dishes on the menu
    And a customer named Olivia who has booked a table for 4 guests

  Rule: Submitting merges the confirmed carts into the owner's pre-order

    @smoke
    Scenario: The dishes of all confirmed carts are merged into the pre-order
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has joined the group pre-order
      And the guest Noah has put 1 portion of dish 1 in their cart
      And the guest Noah has put 1 portion of dish 2 in their cart
      And the guest Noah has submitted their cart
      When Olivia submits the group pre-order
      Then the submitted dishes are:
        | dish | portions |
        | 1    | 3        |
        | 2    | 1        |
      When Olivia looks at their order
      Then the order holds:
        | dish | portions |
        | 1    | 4        |
        | 2    | 1        |

    @negative
    Scenario: A cart that was not confirmed is left out
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 2 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has joined the group pre-order
      And the guest Noah has put 3 portions of dish 1 in their cart
      When Olivia submits the group pre-order
      Then the submitted dishes are:
        | dish | portions |
        | 2    | 1        |
      When Olivia looks at their order
      Then the order holds:
        | dish | portions |
        | 1    | 1        |
        | 2    | 1        |

    Scenario: A submitted group pre-order is marked as submitted
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart
      When Olivia submits the group pre-order
      And anyone checks how many people have joined
      Then the group status is Submitted
      When Olivia looks at their order
      Then the order is marked as sent to the restaurant

    @waiter
    Scenario: The waiter sees the dishes the owner submitted
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 2 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has joined the group pre-order
      And the guest Noah has put 1 portion of dish 1 in their cart
      When Olivia submits the group pre-order
      And the waiter looks at the reservation
      Then the waiter sees these dishes in the pre-order:
        | dish | portions |
        | 1    | 1        |
        | 2    | 1        |

  Rule: Nothing can be added to a group pre-order that was submitted

    Background:
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart
      And the guest Liam has joined the group pre-order
      And Olivia has submitted the group pre-order

    @negative
    Scenario: The owner cannot submit the group pre-order twice
      When Olivia submits the group pre-order
      Then the request is rejected saying "Group reservation was submitted."

    @negative
    Scenario: A guest cannot put more dishes in a cart once the group is submitted
      When the guest Liam puts 1 portion of dish 2 in their cart
      Then the request is refused as a conflict saying "Group reservation was submitted."

    @negative
    Scenario: A new guest cannot join once the group is submitted
      When the guest Noah joins the group pre-order
      Then the request is rejected saying "Group reservation was submitted."

  Rule: The group pre-order is only submitted when it makes sense

    @negative
    Scenario: The owner cannot submit when nobody has confirmed a cart
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      When Olivia submits the group pre-order
      Then the request is rejected saying "There are no submitted guest sessions in the group."

    @negative
    Scenario: The owner cannot submit when nobody has joined
      Given Olivia has placed their own pre-order of dish 1
      When Olivia submits the group pre-order
      Then the request is rejected saying "There are no submitted guest sessions in the group."

    @negative
    Scenario: The owner cannot submit before placing their own pre-order
      Given the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart
      When Olivia submits the group pre-order
      Then the request is rejected

    Scenario: The submission has to say which reservation it is for
      Given Olivia has placed their own pre-order of dish 1
      When Olivia submits the group pre-order without saying which reservation it is for
      Then the request is rejected saying "'reservationId' is required."

    @negative
    Scenario: The submission cannot be for a reservation that does not exist
      Given Olivia has placed their own pre-order of dish 1
      When Olivia submits the group pre-order for a reservation that does not exist
      Then they are told it could not be found

  Rule: Only the owner of the reservation submits the group pre-order

    Background:
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart

    @negative
    Scenario: Another customer cannot submit it
      Given another customer named Peter is signed in
      When Peter submits the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: A waiter cannot submit it
      When the waiter submits the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: Someone who is not signed in cannot submit it
      When someone who is not signed in submits the group pre-order
      Then they are turned away because they are not signed in

  Rule: A guest can only confirm a cart that has something in it, once

    @negative
    Scenario: A guest cannot confirm an empty cart
      Given the guest Mia has joined the group pre-order
      When the guest Mia submits their cart
      Then the request is rejected

    @negative
    Scenario: A guest cannot confirm the same cart twice
      Given the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart
      When the guest Mia submits their cart
      Then the request is refused as a conflict saying "Guest session is already submitted."

    @negative
    Scenario: A guest cannot put zero portions in the cart
      Given the guest Mia has joined the group pre-order
      When the guest Mia puts 0 portions of dish 1 in their cart
      Then the request is rejected saying "Quantity must be greater than zero"

    @negative
    Scenario: A guest cannot put dishes in another guest's cart
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      When the guest Mia puts 1 portion of dish 1 in the cart of Noah
      Then the guest is told that their session is not valid

  Rule: The group cannot grow beyond the number of guests booked

    @negative
    Scenario: A guest cannot join a group that is full
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Zoe has joined the group pre-order
      When the guest Ivy joins the group pre-order
      Then the request is rejected saying "Guest limit exceeded"
